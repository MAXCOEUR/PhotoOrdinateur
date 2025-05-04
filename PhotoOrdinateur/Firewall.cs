using WindowsFirewallHelper;
using WindowsFirewallHelper.FirewallRules;

public class Firewall
{

    static string ruleName = "PhotoSyncServer Firewall Rule";

    public static void AddFirewallRuleForApp(int port)
    {
        var rule = FirewallManager.Instance.CreatePortRule(
             FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public, // Tous les profils
             ruleName,
             FirewallAction.Allow,
             (ushort)port,
             FirewallProtocol.TCP
         );

        rule.Direction = FirewallDirection.Inbound;

        // Vérifie si la règle existe déjà pour éviter les doublons
        var isexiste = FirewallManager.Instance.Rules.FirstOrDefault(r => r.Name == ruleName) == null;
        if (isexiste)
        {
            FirewallManager.Instance.Rules.Add(rule);
        }
    }

    public static void RemoveFirewallRuleForApp()
    {

        var rule = FirewallManager.Instance.Rules
            .FirstOrDefault(r => r.Name == ruleName);

        if (rule != null)
        {
            FirewallManager.Instance.Rules.Remove(rule);
        }
    }
}
