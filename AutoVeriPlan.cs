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

            List<ExternalPlanSetup> selectedPlans = PlanSelector(p);
            if (!selectedPlans.Any()) { return; } // if there are no selected plans, end.

            CreateVerificationPlans(p, selectedPlans, QAcourseId, QADetails, calcOptions);
        }

        /// <summary>
        /// Get user input for which plans corresponding verification plans must be made.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static List<ExternalPlanSetup> PlanSelector(Patient p)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Please select plans enumerated below in the format \"#.#\", space-delimited, no trailing whitespace.");
            int courseCounter = 1;
            foreach (Course course in p.Courses)
            {
                int planCounter = 1;
                sb.AppendLine($"{courseCounter} {course.Id} {course.ClinicalStatus}");
                foreach (PlanSetup plan in course.PlanSetups)
                {
                    sb.AppendLine($"- {courseCounter}.{planCounter} {plan.Id}");
                    planCounter++;
                }
                courseCounter++;
            }

            string selectedPlansString;
            string pattern = @"^(\d+\.\d+)( \d+\.\d+)*$";
            List<ExternalPlanSetup> selectedPlans = new List<ExternalPlanSetup>();
            while (true)
            {
                selectedPlansString = Interaction.InputBox(sb.ToString(), "Select Plans");
                if (Regex.IsMatch(selectedPlansString, pattern))
                {
                    bool failFlag = false;
                    foreach (var entry in selectedPlansString.Split(' '))
                    {
                        var indices = entry.Split('.');
                        int courseIndex = Int32.Parse(indices[0]);
                        int planIndex = Int32.Parse(indices[1]);
                        if (courseIndex - 1 >= 0 && courseIndex - 1 < p.Courses.Count() &&
                            planIndex - 1 >= 0 && planIndex - 1 < p.Courses.ElementAt(courseIndex - 1).PlanSetups.Count())
                        {
                            Course selectedCourse = p.Courses.ElementAt(courseIndex - 1);
                            ExternalPlanSetup selectedPlan = selectedCourse.ExternalPlanSetups.ElementAt(planIndex - 1);
                            selectedPlans.Add(selectedPlan);
                        }
                        else
                        {
                            MessageBox.Show($"Index out of bounds for entry {entry}. Please try again.");
                            failFlag = true;
                        }
                    }
                    if (failFlag) { continue; }
                    break;
                }
                else if (selectedPlansString == "")
                {
                    return selectedPlans;
                }
                else
                {
                    MessageBox.Show("Improperly formatted selection. Ensure that all plan selections are of the form #.# and separated only by spaces.");
                }
            }
            return selectedPlans;
        }

        /// <summary>
        /// Iterates over verification plans and creates a verification plan for each.
        /// </summary>
        /// <param name="patient"></param>
        /// <param name="selectedPlans"></param>
        /// <param name="QAcourseId"></param>
        /// <param name="QADetails"></param>
        /// <param name="calcOptions"></param>
        /// <exception cref="ApplicationException"></exception>
        public void CreateVerificationPlans(Patient patient,
                                            List<ExternalPlanSetup> selectedPlans,
                                            string QAcourseId,
                                            Dictionary<string, Dictionary<string, string>> QADetails,
                                            Dictionary<string, Dictionary<string, string>> calcOptions)
        {
            foreach (ExternalPlanSetup plan in selectedPlans)
            {
                string model = plan.PhotonCalculationModel;
                if (!QADetails.ContainsKey(model)) { throw new ApplicationException($"Plan {plan.Name}'s calculation model {model} doesn't have a QA structure set defined. Modify the code to create the necessary behavior and recompile."); }
                string QAPatientID = QADetails[model]["QAPatientID"];
                string QAStudyID = QADetails[model]["QAStudyID"];
                string QAImageID = QADetails[model]["QAImageID"];

                Course QAcourse = patient.Courses.Where(o => o.Id == QAcourseId).SingleOrDefault();
                if (QAcourse == null) // if there isn't a qa course, make one.
                {
                    QAcourse = patient.AddCourse();
                    QAcourse.Id = QAcourseId;
                }
                else // only if a prior qa course does exist
                {
                    if (!QAcourse.ClinicalStatus.Equals(CourseClinicalStatus.Active)) // anything other than active
                    {
                        throw new ApplicationException("QA course is not active. Please activate QA course.");
                    }
                }

                StructureSet ssQA = patient.CopyImageFromOtherPatient(QAPatientID, QAStudyID, QAImageID);

                ExternalPlanSetup verificationPlan = CreateVerificationPlan(QAcourse, plan, ssQA, calcOptions);

                PlanSetup verifiedPlan = verificationPlan.VerifiedPlan;
                if (plan != verifiedPlan) { throw new ApplicationException($"Error: verified plan {verifiedPlan.Id} != loaded plan {plan.Id}!"); }
            }
        }
        /// <summary>
        /// Create verifications plans for a given treatment plan. This was mostly edited from LDClark's CreateVerificationPlan.
        /// </summary>
        /// <param name="course"></param>
        /// <param name="verifiedPlan"></param>
        /// <param name="verificationStructures"></param>
        /// <param name="calcOptions"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ApplicationException"></exception>
        public static ExternalPlanSetup CreateVerificationPlan(Course course,
                                                               ExternalPlanSetup verifiedPlan,
                                                               StructureSet verificationStructures,
                                                               Dictionary<string, Dictionary<string, string>> calcOptions)
        {
            var verificationPlan = course.AddExternalPlanSetupAsVerificationPlan(verificationStructures, verifiedPlan);

            try
            {
                string verificationId = "";
                if (verifiedPlan.Id.Length >= 13)  //if over 12 chars in Id
                    verificationId = verifiedPlan.Id.Substring(0, (verifiedPlan.Id.Length - 3)) + "_QA";
                else
                    verificationId = verifiedPlan.Id + "_QA";
                verificationPlan.Id = verificationId;
            }
            catch (System.ArgumentException)
            {
                var message = "Plan already exists in QA course.";
                throw new Exception(message);
            }

            // Put isocenter to the center of the QAdevice
            VVector isocenter = verificationPlan.StructureSet.Image.UserOrigin;
            var beamList = verifiedPlan.Beams.ToList(); //used for looping later
            foreach (Beam beam in verifiedPlan.Beams)
            {
                if (beam.IsSetupField)
                    continue;

                ExternalBeamMachineParameters MachineParameters =
                    new ExternalBeamMachineParameters(beam.TreatmentUnit.Id, beam.EnergyModeDisplayName, beam.DoseRate, beam.Technique.Id, string.Empty);

                if (beam.MLCPlanType.ToString() == "VMAT")
                {
                    // Create a new VMAT beam.
                    var collimatorAngle = beam.ControlPoints.First().CollimatorAngle;
                    var gantryAngleStart = beam.ControlPoints.First().GantryAngle;
                    var gantryAngleEnd = beam.ControlPoints.Last().GantryAngle;
                    var gantryDirection = beam.GantryDirection;
                    var metersetWeights = beam.ControlPoints.Select(cp => cp.MetersetWeight);
                    verificationPlan.AddVMATBeam(MachineParameters, metersetWeights, collimatorAngle, gantryAngleStart,
                        gantryAngleEnd, gantryDirection, 0.0, isocenter);
                    continue;
                }
                else if (beam.MLCPlanType.ToString() == "DoseDynamic")
                {
                    // Create a new IMRT beam.
                    double gantryAngle = beam.ControlPoints.First().GantryAngle;
                    double collimatorAngle = beam.ControlPoints.First().CollimatorAngle;
                    var metersetWeights = beam.ControlPoints.Select(cp => cp.MetersetWeight);
                    verificationPlan.AddSlidingWindowBeam(MachineParameters, metersetWeights, collimatorAngle, gantryAngle,
                        0.0, isocenter);
                    continue;
                }
                else
                {
                    var message = string.Format("Treatment field {0} is not VMAT or IMRT.", beam);
                    throw new Exception(message);
                }
            }

            int i = 0;
            foreach (Beam verificationBeam in verificationPlan.Beams)
            {
                verificationBeam.Id = beamList[i].Id;
                i++;
            }

            foreach (Beam verificationBeam in verificationPlan.Beams)
            {
                foreach (Beam verifiedBeam in verifiedPlan.Beams)
                {
                    //if (verificationBeam.Id == beam.Id && verificationBeam.MLCPlanType.ToString() == "DoseDynamic")
                    if (verificationBeam.Id == verifiedBeam.Id)
                    {
                        var verifiedBeamEditableParams = verifiedBeam.GetEditableParameters();
                        verifiedBeamEditableParams.Isocenter = verificationPlan.StructureSet.Image.UserOrigin;
                        verificationBeam.ApplyParameters(verifiedBeamEditableParams);
                        continue;
                    }
                }
            }

            verificationPlan.PlanNormalizationValue = verifiedPlan.PlanNormalizationValue;

            // Set presciption
            const int numberOfFractions = 1;
            verificationPlan.SetPrescription(numberOfFractions, verifiedPlan.DosePerFraction, verifiedPlan.TreatmentPercentage);

            verificationPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, verifiedPlan.GetCalculationModel(CalculationType.PhotonVolumeDose));
            if (calcOptions.ContainsKey(verificationPlan.GetCalculationModel(CalculationType.PhotonVolumeDose)))
            {
                foreach (var ModelDictPair in calcOptions)
                {
                    string model = ModelDictPair.Key;
                    foreach (var OptionValuePair in ModelDictPair.Value)
                    {
                        string option = OptionValuePair.Key;
                        string value = OptionValuePair.Value;
                        // SetCalculationOption returns false if it can't find the setting to set, and we want that to throw an error because we can't have that slip by.
                        // Testing this conditional does actually set the calculation option, though.
                        if (!verificationPlan.SetCalculationOption(model, option, value)) { throw new ApplicationException($"Couldn't set setting {model},{option},{value}. Exiting."); }
                    }
                }
            }

            CalculationResult res;
            if (verificationPlan.Beams.FirstOrDefault().MLCPlanType.ToString() == "DoseDynamic") //imrt
            {
                var getCollimatorAndGantryAngleFromBeam = verifiedPlan.Beams.Count() > 1;
                var presetValues = (from beam in verifiedPlan.Beams
                                    select new KeyValuePair<string, MetersetValue>(beam.Id, beam.Meterset)).ToList();
                res = verificationPlan.CalculateDoseWithPresetValues(presetValues);
            }
            else //vmat
                res = verificationPlan.CalculateDose();
            if (!res.Success)
            {
                var message = string.Format("Dose calculation failed for verification plan. Output:\n{0}", res);
                throw new Exception(message);
            }
            return verificationPlan;
        }
    }
}
