//go:build windows

package main

import (
    "fmt"
    "path/filepath"
    "sort"
    "strings"
)

// installCandidate describes a verified host-to-payload mapping, not proof of
// successful AutoCAD runtime acceptance.
type installCandidate struct {
    Host cadHost
    Payload string
    Selectable bool
    Reason string
}

// planCadInstallations intentionally fails closed: the discovered host's
// exact flavor needs a separately built and integrity-validated payload.
// A 2023-only package must never be offered for another AutoCAD release.
// NAMVECTA V2.0 is deliberately limited to AutoCAD 2020-2024, even when
// legacy SDK classification still detects a different AutoCAD generation.
func planCadInstallations(hosts []cadHost, validatedPayloads map[string]string) []installCandidate {
    result:=make([]installCandidate,0,len(hosts))
    seen:=make(map[string]bool)
    for _,h:=range hosts {
        if h.Year<2020 || h.Year>2024 { continue }
        key:=strings.ToLower(filepath.Clean(h.Directory))
        if seen[key] {continue}
        seen[key]=true
        c:=installCandidate{Host:h}
        switch {
        case h.Error!="":
            c.Reason=h.Error
        case h.Flavor=="":
            c.Reason="Không xác định bản DLL tương thích"
        case validatedPayloads[h.Flavor]=="":
            c.Reason="Chưa có module đã kiểm tra cho "+h.Flavor
        default:
            c.Selectable=true
            c.Payload=validatedPayloads[h.Flavor]
        }
        result=append(result,c)
    }
    sort.SliceStable(result,func(i,j int)bool {
        if result[i].Host.Year!=result[j].Host.Year{return result[i].Host.Year<result[j].Host.Year}
        return strings.ToLower(result[i].Host.Directory)<strings.ToLower(result[j].Host.Directory)
    })
    return result
}

func validateCadSelection(plan []installCandidate, selectedDirectories []string) ([]installCandidate,error) {
    if len(selectedDirectories)==0 {return nil,fmt.Errorf("Chưa chọn phiên bản AutoCAD để cài đặt")}
    byDirectory:=make(map[string]installCandidate,len(plan))
    for _,c:=range plan {byDirectory[strings.ToLower(filepath.Clean(c.Host.Directory))]=c}
    seen:=map[string]bool{}
    chosen:=make([]installCandidate,0,len(selectedDirectories))
    for _,path:=range selectedDirectories {
        key:=strings.ToLower(filepath.Clean(path))
        if seen[key] {return nil,fmt.Errorf("Phiên bản AutoCAD được chọn trùng lặp")}
        seen[key]=true
        c,found:=byDirectory[key]
        if !found || !c.Selectable || c.Payload=="" {return nil,fmt.Errorf("AutoCAD không có module hợp lệ: %s",path)}
        chosen=append(chosen,c)
    }
    return chosen,nil
}
