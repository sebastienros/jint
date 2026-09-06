#nullable enable

using System.Text;

namespace Jint.Tests.Browser.Layout;

/// <summary>A script-free admin form: nested layout wrappers, inherited theme tokens and grouped selectors.</summary>
/// <remarks>
/// A reduction, not a captured Orchard response. The Save classes and wrapper patterns come from
/// https://github.com/OrchardCMS/OrchardCore/tree/59dd4923007afb36220897b74524b608ee23e682/src/OrchardCore.Modules/OrchardCore.Settings/Views.
/// The generated theme tokens and rules keep the depth-sensitive cascade workload offline and deterministic.
/// </remarks>
internal static class AdminSettingsDocument
{
    internal static string Create(bool saved = false, string? css = null)
    {
        var html = new StringBuilder("<!doctype html><html data-bs-theme='light'><head><style>");
        if (css is not null)
        {
            html.Append(css);
        }
        else
        {
            html.Append(":root,[data-bs-theme=light]{");
            for (var i = 0; i < 180; i++)
            {
                html.Append("--theme-").Append(i).Append(":").Append(i).Append("px;");
            }

            html.Append("color:#212529;font-family:sans-serif} ");
            for (var i = 0; i < 200; i++)
            {
                html.Append(".unused-").Append(i).Append(",.admin-menu .item-").Append(i)
                    .Append(":not(.active)>a{padding:0.5rem;color:var(--theme-0)} ");
            }

            html.Append("""
                .form-control{display:block;width:100%;padding:.375rem .75rem}
                .btn{display:inline-block;padding:.375rem .75rem;border:1px solid transparent}
                .btn-primary{color:white;background-color:blue}
                .d-none{display:none!important}
                .navbar,.content,.card,.card-body,.mb-3{display:block}
                """);
        }

        html.Append("""
            </style></head><body class="the-admin"><div class="ta-wrapper">
            <nav class="admin-menu"><ul>
            """);
        for (var i = 0; i < 40; i++)
        {
            html.Append("<li class='nav-item'><a><span><i></i>Menu</span></a></li>");
        }

        html.Append("""
            </ul></nav><main><div class="content"><div class="content-body"><div class="card">
            <div class="card-body"><form method="post"><div class="tab-content"><div class="tab-pane active">
            """);
        for (var i = 0; i < 70; i++)
        {
            html.Append("<div class='mb-3'><div class='row'><div class='col-sm-9'><label>Setting</label>")
                .Append("<input class='form-control' name='setting").Append(i)
                .Append("' value='value'><span class='hint'>Help</span></div></div></div>");
        }

        html.Append("""
            </div></div><div class="edit-item-secondary"><div class="edit-item-actions">
            <button type="submit" class="primaryAction btn btn-primary save">Save</button>
            </div></div></form>
            """);
        if (saved)
        {
            html.Append("<p id='saved'>Settings saved</p>");
        }

        return html.Append("</div></div></div></div></main></div></body></html>").ToString();
    }
}
