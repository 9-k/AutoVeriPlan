using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;
using ESAPIUtilities;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {
            // HERE ARE YOUR USER-DEFINED VARIABLES.
            // CHANGE THEM TO SUIT YOUR LOCATION.
            // not yet implemented
            // string exportDir = @"Q:\Patient QA";
            string QAcourseId = "QA";
            Dictionary<string, Dictionary<string, string>> QADetails = new Dictionary<string, Dictionary<string, string>>
            {
                { "AcurosXB_156MR3", new Dictionary<string, string>
                    {
                        { "QAPatientID", "ArcCheck" },
                        { "QAStudyID", "CT1" },
                        { "QAImageID", "NewAC Acuros" }
                    }
                },
                { "AAA_15606", new Dictionary<string, string>
                    {
                        { "QAPatientID", "ArcCheck" },
                        { "QAStudyID", "CT1" },
                        { "QAImageID", "NewArcCheck AAA"}
                    }
                },
            };
            Dictionary<string, Dictionary<string, string>> calcOptions = new Dictionary<string, Dictionary<string, string>>
            {
                { "AcurosXB_156MR3", new Dictionary<string, string>
                    {
                        { "DoseReportingMode", "Dose to water" },
                    }
                }
            };

            Patient p = context.Patient;

            if (p == null) { throw new ApplicationException("Please load a patient"); }
            if (!p.CanModifyData()) { throw new ApplicationException("Can't modify data in the database."); }

            p.BeginModifications();

            List<ExternalPlanSetup> selectedPlans = ESAPIUtility.PlanSelector(p);
            if (!selectedPlans.Any()) { return; } // if there are no selected plans, end.

            ESAPIUtility.CreateVerificationPlans(p, selectedPlans, QAcourseId, QADetails, calcOptions);
        }
    }
}
