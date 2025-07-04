﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Options
{
    public class PreviewGeneratorOptions : DialogPage
    {
        [Category("Destination")]
        [DisplayName("Local Nuget Repository Folder")]
        [Description("This is the folder where generated preview NuGet packages will be published to, be sure to configure it in under Package Sources in the Package Manager settings.")]
        public string DestinationNugetPreviewSource
        {
            get;
            set;
        }

        [Category("Destination")]
        [DisplayName("Clear Local Repository Before Build")]
        [Description("If enabled, all packages in the local Nuget repository will be removed before building new packages. Use with caution.")]
        public bool ClearLocalRepositoryBeforeBuild { get; set; } = false;


        [Category("General")]
        [DisplayName("Build Configuration")]
        [Description("Specifies the build configuration to use (e.g., Debug, Release).")]
        public string BuildConfiguration { get; set; } = "Debug";

        [Category("General")]
        [DisplayName("Clean before build")]
        [Description("Specify whether to perform a clean operation before building the projects.")]
        public bool CleanBeforeBuild { get; set; } = true;


        [Category("General")]
        [DisplayName("Run parallel")]
        [Description("Run the package generation in parallel.")]
        public bool Parallel { get; set; } = true;

        [Category("General")]
        [DisplayName("Maximum Degree of Parallelism")]
        [Description("Maximum number of parallel tasks to run.")]
        public int MaxDegreeOfParallelism { get; set; } = 4;

    }
}
