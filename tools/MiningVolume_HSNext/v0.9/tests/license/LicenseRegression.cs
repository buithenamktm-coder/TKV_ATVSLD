using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiningVolume2023.Services;

// These platform APIs must NEVER be mistaken for verified DPAPI tests.
// Throwing stubs allow only the production ledger reconciliation to run here.
namespace System.Security.Cryptography {
    public enum DataProtectionScope { CurrentUser }
    public static class ProtectedData {
        public static byte[] Protect(byte[] a, byte[] b, DataProtectionScope c) => throw new PlatformNotSupportedException();
        public static byte[] Unprotect(byte[] a, byte[] b, DataProtectionScope c) => throw new PlatformNotSupportedException();
    }
}
class LicenseRegression {
    static DateTime Day(int n) => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(n);
    static ActivationLedger Ledger(string tier, int seen, params (string, int)[] history) => new ActivationLedger {
        ActiveTier=tier, LastSeenUtc=Day(seen), History=history.Select(x=>new UsedPass {Tier=x.Item1,FirstActivationUtc=Day(x.Item2)}).ToList()
    };
    static void Check(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
    static void Invalid(Action action) { try { action(); } catch(InvalidDataException) { return; } throw new Exception("Invalid record accepted"); }
    static void Main() {
        int count=0;
        Action<string,Action> test=(name,run)=> {run(); count++; Console.WriteLine("PASS "+name);};
        test("empty stores",()=>Check(LicenseService.MergeActivation(null,null)==null && LicenseService.MergeTrial(null,null)==null));
        test("interrupted tier change retains all first-use dates",()=>{
            var old=Ledger("1 tháng",10,("1 tháng",0));
            var newer=Ledger("3 tháng",20,("1 tháng",5),("3 tháng",15));
            foreach(var reverse in new[]{false,true}) {
                var r=LicenseService.MergeActivation(reverse?newer:old,reverse?old:newer);
                Check(r.ActiveTier=="3 tháng" && r.LastSeenUtc==Day(20) && r.History.Count==2);
                Check(r.History.Single(x=>x.Tier=="1 tháng").FirstActivationUtc==Day(0));
            }
        });
        test("all five tiers retain earliest date",()=>{
            foreach(var tier in new[]{"1 tháng","3 tháng","6 tháng","1 năm","Vĩnh viễn"}) {
                var r=LicenseService.MergeActivation(Ledger(tier,20,(tier,0)),Ledger(tier,30,(tier,10)));
                Check(r.History.Single().FirstActivationUtc==Day(0));
            }
        });
        test("trial merge cannot renew or lose clock watermark",()=>{
            var a=new TrialLedger {FirstUseUtc=Day(0),LastUseUtc=Day(2)};
            var b=new TrialLedger {FirstUseUtc=Day(1),LastUseUtc=Day(20)};
            foreach(var reverse in new[]{false,true}) {
                var r=LicenseService.MergeTrial(reverse?b:a,reverse?a:b);
                Check(r.FirstUseUtc==Day(0) && r.LastUseUtc==Day(20));
            }
        });
        test("equal-time conflicting active tiers fail closed",()=>Invalid(()=>LicenseService.MergeActivation(Ledger("1 tháng",10,("1 tháng",0)),Ledger("3 tháng",10,("3 tháng",0)))));
        test("duplicate history fails closed",()=>Invalid(()=>LicenseService.MergeActivation(Ledger("1 tháng",10,("1 tháng",0),("1 tháng",1)),null)));
        test("missing active history fails closed",()=>Invalid(()=>LicenseService.MergeActivation(Ledger("1 tháng",10,("3 tháng",0)),null)));
        test("unknown tier fails closed",()=>Invalid(()=>LicenseService.MergeActivation(Ledger("unknown",10,("unknown",0)),null)));
        test("future first activation fails closed",()=>Invalid(()=>LicenseService.MergeActivation(Ledger("1 tháng",10,("1 tháng",11)),null)));
        test("null history item fails closed",()=>{
            var a=Ledger("1 tháng",10,("1 tháng",0));a.History.Add(null);
            Invalid(()=>LicenseService.MergeActivation(a,null));
        });
        test("invalid trial fails closed",()=>Invalid(()=>LicenseService.MergeTrial(new TrialLedger {FirstUseUtc=Day(1),LastUseUtc=Day(0)},null)));
        test("merge is idempotent and does not mutate inputs",()=>{
            var a=Ledger("1 tháng",10,("1 tháng",0));
            var b=Ledger("3 tháng",20,("3 tháng",15));
            var r=LicenseService.MergeActivation(a,b);
            var again=LicenseService.MergeActivation(r,r);
            Check(again.History.Count==2 && again.ActiveTier==r.ActiveTier && a.History.Count==1 && b.History.Count==1);
        });
        Console.WriteLine(count+" ledger regression tests passed; Windows storage/host NOT TESTED.");
    }
}
