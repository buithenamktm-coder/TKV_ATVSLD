//go:build windows

package main

import "testing"

func TestHostInstallPlanNeverMaps2023BinaryToOtherCAD(t *testing.T) {
    hosts:=[]cadHost{
        {Directory:`C:\AutoCAD2020`,Year:2020,Flavor:"ACAD2020_NET47"},
        {Directory:`C:\AutoCAD2023`,Year:2023,Flavor:"ACAD2023_NET48"},
        {Directory:`C:\AutoCAD2025`,Year:2025,Flavor:"ACAD2025_NET8"},
        {Directory:`C:\AutoCAD2026`,Year:2026,Flavor:"ACAD2026_NET10"},
    }
    plan:=planCadInstallations(hosts,map[string]string{"ACAD2023_NET48":"bundle/2023"})
    if len(plan)!=2 {t.Fatalf("Expected only 2020 and 2023 hosts, got %d",len(plan))}
    for _,c:=range plan {
        if c.Host.Year==2023 {
            if !c.Selectable || c.Payload!="bundle/2023" {t.Fatal("2023 valid payload missing")}
        } else if c.Selectable || c.Payload!="" || c.Reason=="" {
            t.Fatalf("Unsupported host incorrectly selectable: %+v",c)
        }
    }
    if _,err:=validateCadSelection(plan,[]string{`C:\AutoCAD2025`});err==nil {
        t.Fatal("2025 must not reuse 2023 payload")
    }
    if got,err:=validateCadSelection(plan,[]string{`C:\AutoCAD2023`});err!=nil||len(got)!=1 {
        t.Fatal("2023 should be selectable",err)
    }
}

func TestHostInstallPlanDuplicateAndErrorFailClosed(t *testing.T) {
    plan:=planCadInstallations([]cadHost{
        {Directory:`C:\CAD`,Year:2024,Flavor:"ACAD2024_NET48",Error:"Invalid acad.exe"},
        {Directory:`c:\cad`,Year:2024,Flavor:"ACAD2024_NET48"},
        {Directory:`D:\CAD26`,Year:2026,Flavor:"ACAD2026_NET10"},
        {Directory:`D:\CAD19`,Year:2019,Flavor:"ACAD2019_NET47"},
    },map[string]string{"ACAD2024_NET48":"verified/2024","ACAD2026_NET10":"verified/2026"})
    if len(plan)!=1 || plan[0].Selectable {t.Fatalf("Unsafe plan: %+v",plan)}
    if _,err:=validateCadSelection(plan,nil);err==nil {t.Fatal("Empty selection allowed")}
    if _,err:=validateCadSelection(plan,[]string{`D:\CAD26`,`d:\cad26`});err==nil {t.Fatal("Duplicate selected twice")}
    if _,err:=validateCadSelection(plan,[]string{`D:\CAD26`,`C:\CAD`});err==nil {t.Fatal("Partial unsafe selection accepted")}
}

func TestNamvectaV2OnlyOffersExactly2020To2024(t *testing.T) {
    hosts:=[]cadHost{
        {Directory:`C:\\CAD2019`,Year:2019,Flavor:"ACAD2019_NET47"},
        {Directory:`C:\\CAD2020`,Year:2020,Flavor:"ACAD2020_NET47"},
        {Directory:`C:\\CAD2021`,Year:2021,Flavor:"ACAD2021_NET48"},
        {Directory:`C:\\CAD2022`,Year:2022,Flavor:"ACAD2022_NET48"},
        {Directory:`C:\\CAD2023`,Year:2023,Flavor:"ACAD2023_NET48"},
        {Directory:`C:\\CAD2024`,Year:2024,Flavor:"ACAD2024_NET48"},
        {Directory:`C:\\CAD2025`,Year:2025,Flavor:"ACAD2025_NET8"},
        {Directory:`C:\\CAD2026`,Year:2026,Flavor:"ACAD2026_NET10"},
    }
    payload:=map[string]string{}
    for _,h:=range hosts {payload[h.Flavor]="verified/"+h.Flavor}
    plan:=planCadInstallations(hosts,payload)
    if len(plan)!=5 {t.Fatalf("Only 2020..2024 are in scope, got %d entries",len(plan))}
    for i,c:=range plan {
        if !c.Selectable || c.Host.Year!=2020+i {t.Fatalf("Host should be selectable: %+v",c)}
    }
    for _,excluded:=range []string{`C:\\CAD2019`,`C:\\CAD2025`,`C:\\CAD2026`} {
        if _,err:=validateCadSelection(plan,[]string{excluded});err==nil {
            t.Fatalf("Unsupported host accepted: %s",excluded)
        }
    }
}
