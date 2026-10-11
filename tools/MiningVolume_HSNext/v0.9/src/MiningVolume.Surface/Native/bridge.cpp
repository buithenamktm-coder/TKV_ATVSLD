// IMSAT C ABI adapter; CDT headers and their original notices are in vendor/CDT.
#define CDT_ENABLE_CALLBACK_HANDLER
#include "CDT.h"
#include <cstring>
#include <limits>
#include <memory>
#include <stdexcept>
#ifdef _WIN32
#define API extern "C" __declspec(dllexport)
#else
#define API extern "C" __attribute__((visibility("default")))
#endif
using Check = int (*)(int, int, int);
struct Observer : CDT::ICallbackHandler {
    Check check; int phase=0, done=0, total;
    Observer(Check c,int n):check(c),total(n){}
    bool isAbortCalculation()const override {return check && check(phase,done,total)!=0;}
    void onAddVertexStart(CDT::VertInd,CDT::AddVertexType::Enum) override {++done;}
    void onAddEdgeStart(const CDT::Edge&) override {++done;}
};
struct Mesh {std::vector<int> indices;};
static void setError(char* out,int capacity,const char* message) {
    if(out && capacity>0){std::strncpy(out,message,capacity-1);out[capacity-1]=0;}
}
API int mv_cdt_build(const double* xy,int count,const int* edges,int edgeCount,
    Check check,void** result,int* triangles,char* error,int capacity) {
    if(!result || !triangles)return 1;
    *result=nullptr;*triangles=0;
    try {
        if(!xy || count<3 || edgeCount<0 || (edgeCount>0&&!edges))
            throw std::invalid_argument("Invalid CDT input buffer");
        Observer observer(check,count);if(observer.isAbortCalculation())return 2;
        CDT::Triangulation<double> cdt(CDT::VertexInsertionOrder::Auto,
            CDT::IntersectingConstraintEdges::NotAllowed,0.0);
        cdt.setCallbackHandler(&observer);
        std::vector<CDT::V2d<double>> points;points.reserve(count);
        for(int i=0;i<count;i++) points.emplace_back(xy[2*i],xy[2*i+1]);
        cdt.insertVertices(points);if(observer.isAbortCalculation())return 2;
        observer.phase=1;observer.done=0;observer.total=edgeCount;
        std::vector<CDT::Edge> constraints;constraints.reserve(edgeCount);
        for(int i=0;i<edgeCount;i++){
            int a=edges[2*i],b=edges[2*i+1];
            if(a<0||b<0||a>=count||b>=count||a==b)throw std::invalid_argument("Invalid constraint index");
            constraints.emplace_back(a,b);
        }
        cdt.insertEdges(constraints);if(observer.isAbortCalculation())return 2;
        cdt.eraseSuperTriangle();
        if(cdt.vertices.size()!=static_cast<size_t>(count))
            throw std::runtime_error("Unexpected generated vertex; normalization is required");
        if(cdt.triangles.size()>static_cast<size_t>(std::numeric_limits<int>::max()/3))
            throw std::runtime_error("Triangle buffer exceeds supported size");
        observer.phase=2;observer.done=0;observer.total=static_cast<int>(cdt.triangles.size());
        CDT::EdgeUSet meshEdges;
        auto mesh=std::unique_ptr<Mesh>(new Mesh());mesh->indices.reserve(cdt.triangles.size()*3);
        std::vector<bool> referenced(count,false);
        for(const auto& t:cdt.triangles){
            if((observer.done++ & 1023)==0 && observer.isAbortCalculation())return 2;
            for(int j=0;j<3;j++){
                const auto a=t.vertices[j],b=t.vertices[(j+1)%3];
                if(a>=static_cast<unsigned>(count))throw std::runtime_error("Invalid output index");
                mesh->indices.push_back(static_cast<int>(a));referenced[a]=true;meshEdges.insert(CDT::Edge(a,b));
            }
        }
        for(bool used:referenced)if(!used)throw std::runtime_error("CDT omitted an input site");
        for(const auto& edge:cdt.fixedEdges)
            if(!meshEdges.count(edge))throw std::runtime_error("A fixed constraint is absent from output");
        CDT::EdgeUSet splitOriginals;
        for(const auto& piece:cdt.pieceToOriginals){
            if(!meshEdges.count(piece.first))throw std::runtime_error("A constraint piece is absent from output");
            for(const auto& original:piece.second)splitOriginals.insert(original);
        }
        for(const auto& edge:constraints)
            if(!cdt.fixedEdges.count(edge)&&!splitOriginals.count(edge))
                throw std::runtime_error("An input constraint was not recovered");
        if(observer.isAbortCalculation())return 2;
        *triangles=static_cast<int>(cdt.triangles.size());*result=mesh.release();return 0;
    }catch(const std::exception& e){setError(error,capacity,e.what());return 1;}
    catch(...){setError(error,capacity,"Unknown CDT failure");return 1;}
}
API int mv_cdt_copy(void* ptr,int* output,int length) {
    if(!ptr || !output)return 1;
    auto mesh=static_cast<Mesh*>(ptr);
    if(length<0||static_cast<size_t>(length)!=mesh->indices.size())return 1;
    std::copy(mesh->indices.begin(),mesh->indices.end(),output);return 0;
}
API void mv_cdt_free(void* ptr){delete static_cast<Mesh*>(ptr);}
