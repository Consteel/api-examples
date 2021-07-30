using System;
using System.Collections.Generic;
using System.Linq;

using ConSteel.Connection;
using ConSteel.Constants;
using ConSteel.MathObjects;
using ConSteel.ModelObjects;
using ConSteel.ModelObjects.Geometry;
using ConSteel.ModelObjects.Material;
using ConSteel.ModelObjects.Sections;
using ConSteel.ModelObjects.Structural;
using ConSteel.ModelObjects.Load;
using ConSteel.ModelObjects.Calculation.Design;
using ConSteel.ModelObjects.Result;
using ConSteel.Serialization;
using ConSteel.Serialization.CSDBDeserialization;

namespace BuildAndCalculate
{

    class Program
    {
        public static readonly Guid source = new Guid("dea57b9f-427d-4cd6-abf5-b73fd0c75a9f");
        static void Main(string[] args)
        {
            // Load in a preexisting model for default layer, loadcombination and material definitions
            var defaultModel = ConnectionHandler.LoadImportFile("DefaultModel.smadsteel", out IErrorLog errors);

            // if there were errors in the loaded in model file, errorlogs provide further information
            if (errors.Errors.Count > 0)
                throw new ApplicationException();

            CreateBeams(out Beam col, out Beam beam);

            var support = new Support()
            {
                Name = "Fix",
                W = double.PositiveInfinity,
                X = double.PositiveInfinity,
                Y = double.PositiveInfinity,
                Z = double.PositiveInfinity,
                XX = double.PositiveInfinity,
                YY = double.PositiveInfinity,
                ZZ = double.PositiveInfinity,
            };

            var pointSupport = new SupportPoint()
            {
                StructuralObject = col,
                CSPoint = col.Edge.StartNode,
                Support = support,
                DirectionType = eDirectionType.Local
            };

            var load = new LineLoad()
            {
                Z1 = -5,
                Z2 = -5,
                Edge = beam.Edge,
                LoadCase = defaultModel.GetObjectsByType<LoadCase>().First(),
                StructuralObject = beam,
                DirectionType = eLoadDirectionType.Local
            };

            var loadComb = defaultModel.GetObjectsByType<LoadCombination>().First();
            loadComb.Calculate = true;
            loadComb.SecondOrder = true;
            loadComb.BucklingSensitivity = true;
            loadComb.EigenValueNum = 10;

            var portion = new ModelPortion()
            {
                Name = "Teszt portion",
                CornerType = FrameCornerType.Warping_Zero
            };
            portion.Items.Add(col);
            portion.Items.Add(beam);

            var frameCornerSettings = new FrameCornerWizard()
            {
                On = true,
            };
            frameCornerSettings.Portions.Add(portion);

            var designSettings = new DesignSettings()
            {
                SteelCrossSectionCheckPortion = portion,
                SteelCrossSectionCheck = true,
                SteelCrossSectionGm1 = true,
                SteelCrossSectionBucklingCheck = true,
                BucklingCheckPortion = portion,
                BucklingFactor = BucklingFactor.Auto,
                SteelSectionBucklingReductionFactor = KhiReductionFactor.InterpolateBetween_Khi_KhiLT,
                SteelSectionBucklingUltimateFactor = AlphaUltKFactor.SmallestPerBeam,
                EN1993_1_3_Check = true,
                Order = DesignOrder.SecondOrder,
            };
            designSettings.Combinations.Add(loadComb);

            var globalSettings = new GlobalSettingsObject()
            {
                BukclingPortion = portion,
                SelfWeightCase = defaultModel.GetObjectsByType<LoadCase>().First(),
            };

            var model = new List<IModelObject>
            {
                globalSettings,
                designSettings,
                loadComb,
                frameCornerSettings,
                col,
                load,
                pointSupport,
                beam,
            };

            var timeOut = TimeSpan.FromSeconds(60);
            while (true)
            {

                Console.WriteLine($"Press any key to send model with {load.Z1} kN load");
                Console.ReadKey();
                ConnectionHandler.SaveToModel(model, defaultOwnerGuid: source);
                Console.WriteLine("Model sent");

                Console.WriteLine("Press any key to start analysis");
                Console.ReadKey();
                ConnectionHandler.StartAnalysis(timeOut);
                Console.WriteLine("Analysis done");

                Console.WriteLine("Press any key to start design");
                Console.ReadKey();
                ConnectionHandler.StartDesign(timeOut);
                Console.WriteLine("Design done");

                var resultModel = ConnectionHandler.GetObjsByType(timeout: timeOut);
                Console.WriteLine("Model with results received");

                var beamResult =
                    resultModel
                    .GetObjectsByType<DesignSteelBeam>()
                    .OrderByDescending(r => r.R_Sign.Max())
                    .First();

                var maxUtilization =
                    beamResult.R_Sign
                    .Zip(beamResult.SignForm, (maxUtil, formula) => new KeyValuePair<float,SteelDesignSubform>(maxUtil, formula))
                    .OrderByDescending(kv => kv.Key)
                    .First();

                Console.WriteLine($"Largest utilization: {maxUtilization.Key}%, dominant calculation: {maxUtilization.Value}");

                load.Z1 *= 1.1;
                load.Z2 *= 1.1;
            }

        }

        static void CreateBeams(out Beam col, out Beam beam)
        {
            // create reference geometry
            var colLine = new Line(
                new Point3D(0, 0, 0),
                new Point3D(0, 0, 3000));

            var beamLine = new Line(
                new Point3D(0, 0, 3000),
                new Point3D(3000, 0, 3200));

            // create a new Steel material
            var material = new Steel()
            {
                Name = "S235",
                Density = 7850,
                Elasticity = 210000,
                Poisson = 0.3,
                Thermal = 0.000012,
                ThermalFire = 0.000014,
                Fy1 = 235,
                Fy2 = 215,
                Fu1 = 360,
                Fu2 = 360,
                ThicknessY = 40,
                ThicknessU = 40,
            };

            // end release stiffnesses
            var release = new Release()
            {
                Name = "Continous",
                W = double.PositiveInfinity,
                X = double.PositiveInfinity,
                Y = double.PositiveInfinity,
                Z = double.PositiveInfinity,
                XX = double.PositiveInfinity,
                YY = double.PositiveInfinity,
                ZZ = double.PositiveInfinity,
            };

            var macro = new WeldedIorH()
            {
                Height = 200,
                FlangeWidth1 = 200,
                FlangeWidth2 = 200,
                ThicknessW = 5,
                ThicknessF1 = 10,
                ThicknessF2 = 10,
            };

            var section = SectionBuilder.BuildSectionByMacro(
                macro,
                material,
                "Rolled I",
                eShapingMethod.ShapingMethod_HotRolled,
                out string sectionProblems);


            col = new Beam(colLine, section, release, release);
            beam = new Beam(beamLine, section, release, release);

            // define tapering on the beams
            var colTapering = new TaperedMember(
                beam: col,
                startHeight: 100,
                endHeight: 300,
                format: TaperingFormat.Bottom,
                eccentricityAdjustment: TaperedEccAdjustment.SecBigger);

            var beamTapering = new TaperedMember(
                beam: beam,
                startHeight: 300,
                endHeight: 100,
                format: TaperingFormat.Bottom,
                eccentricityAdjustment: TaperedEccAdjustment.SecBigger);

            // manually set owner guids denoting this application as the source of the objects.
            // note you can also provide a default owner guid upon serialization that is applied
            // to any objects not yet owned by anyone
            colLine.SetOwnerGuid(source, true);
            material.SetOwnerGuid(source, true);
            section.SetOwnerGuid(source, true);
            release.SetOwnerGuid(source, true);
            col.SetOwnerGuid(source, true);

            return;
        }
    }
}
