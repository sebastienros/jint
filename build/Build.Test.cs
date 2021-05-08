using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    
    [Partition(2)] readonly Partition TestPartition;
    IEnumerable<Project> TestProjects => TestPartition.GetCurrent(new[]
    {
        Solution.Jint_Tests,
        Solution.Jint_Tests_CommonScripts,
        Solution.Jint_Tests_Ecma,
        Solution.Jint_Tests_Test262
    });

    Target Test => _ => _
        .DependsOn(Restore)
        .Partition(() => TestPartition)
        .Executes(() =>
        {

            foreach (var project in TestProjects)
            {
                DotNetTest(s => s
                    .SetProjectFile(project.Path)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore()
                );
            }
        });
}