using System;
using System.Globalization;
using System.IO;
using System.Text;
using Script;
using UnityEngine;

// Two square Basic armor sections; perpendicular contact at the front edge.
// No DuelSandbox, AI, heat, evasion, core redistribution or disconnection damage.
public static class HESectionDamageCalculation
{
    public static void Run()
    {
        var build = new ShipBuildData();
        double size=build.sectionSize, hp=ShipBuildData.ArmorHP[build.armor[0].hpSettingID];
        double armor=ShipBuildData.Thickness[build.armor[0].defenseThicknessSettingID];
        var output=new StringBuilder("weapon\tpower\tpenetration\tradius\tsectionHP\tarmor\tcontactDistance\tadjacentDistance\tHE_contact\tHE_adjacent\tAP_contact\tAP_adjacent\n");
        foreach (var weapon in MvpWeaponCatalog.Entries)
        {
            double power=weapon.Power, radius=weapon.ExplosionRadius;
            double he1=radius>0 ? Math.Min(hp,ProjectileEffects.BlastPower(power,size/2)) : 0;
            double he2=radius>0 && radius>=size ? Math.Min(hp,ProjectileEffects.BlastPower(power,size*1.5)) : 0;
            double remaining=ProjectilePenetration.AfterArmor(power,weapon.Penetration,armor);
            double ap1=Math.Min(hp,power), ap2=remaining>0 ? Math.Min(hp,power) : 0;
            output.Append(weapon.Name);
            foreach (double value in new[] {power,weapon.Penetration,radius,hp,armor,size/2,size*1.5,he1,he2,ap1,ap2})
                output.Append('\t').Append(value.ToString("G17",CultureInfo.InvariantCulture));
            output.AppendLine();
        }
        string path=Path.GetFullPath("../HESectionDamage.tsv");
        File.WriteAllText(path,output.ToString(),new UTF8Encoding(false));
        Debug.Log("HE_SECTION_CALCULATION_PASS "+path+"\n"+output);
    }
}
