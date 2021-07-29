using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConSteel.Connection;
using ConSteel.Serialization;
using ConSteel.MathObjects;
using ConSteel.ModelObjects.Geometry;
using ConSteel.ModelObjects.Material;
using ConSteel.ModelObjects.Sections;
using ConSteel.Constants;
using ConSteel.ModelObjects.Structural;
using ConSteel.ModelObjects.Load;
using ConSteel.ModelObjects;

namespace GeneralUsage
{
    class Program
    {
        static void Main(string[] args)
        {
            // load in default model for predefined material, releas model and other objects
            var defaultModel = ConnectionHandler.LoadImportFile("DefaultModel.smadsteel");

            // query the loaded in model
            var steelsByName = defaultModel.GetObjectsByType<Steel>().ToDictionary(s => s.Name);
            Steel s235 = steelsByName["S 235 EN 10025-2"];

            // create a new macro section
            WeldedIorH macro = new WeldedIorH() { Height = 300 };

            // build actual section from pairing a macro with a material
            Section section = SectionBuilder.BuildSectionByMacro(macro, s235, "H300", eShapingMethod.ShapingMethod_Welded, out string problems);

            // create reference geometry
            Line refLine = new Line(new Point3D(0, 0, 0), new Point3D(0, 1000, 0));

            // define (or query one from default model) end node release stiffness, negative number signifies infinite stiffness.
            Release continous = new Release("Continous", -1, -1, -1, -1, -1, -1, -1);

            // create beam by defining its reference edge, section, and release stiffnesses at the ends
            Beam beam = new Beam(refLine, section, continous, continous);

            // define (or query one from default model) support point stiffness
            Support support = new Support("Fix", eSupportType.SupportPoint, -1, -1, -1, -1, -1, -1, -1);

            // create support point on the beam's start point
            SupportPoint supportPoint = new SupportPoint("P1", beam, refLine, refLine.StartNode.Value, support, Plane.GlobalXY, eEccType.EccType_C, 0, 0, eDirectionType.Global);

            // create new load group
            LoadGroupData.Variable loadGroup = new LoadGroupData.Variable();

            // create new load case
            LoadCase loadCase = new LoadCase(loadGroup);

            // add node load to the load case on the beam's end
            NodeLoad nodeLoad = new NodeLoad(loadCase, beam, refLine.EndNode, z: 10);

            // get the default load combination from the default model
            LoadCombination loadCombination = defaultModel.GetObjectsByType<LoadCombination>().First();

            // add the new loadcase to the combination
            LoadCombItem loadCombItem = new LoadCombItem(loadCombination, loadCase, 1.5);

            // create a collection with the created objects
            // Note it is enough to only include "top level" objects in the list.
            // Any other object needed by the listed objects will be automatically
            // figured out, including the correct ordering of them.
            List<ModelObject> modelObjects = new List<ModelObject>()
            {
                beam, // will also automatically serialize the refLine, its nodes, the section, etc
                loadCombItem, // will also automatically serialize the loadcase, loadcombination and its dependencies
                nodeLoad, // as before
                supportPoint // as before
            };

            // Objects should have an "owner guid" that identifies the software component / project that created them
            // note that you can also set this manually on a per object basis
            Guid ownerGuid = new Guid("d554fd4d-bccd-49c8-aca9-4d33286d77c5");

            // save out the model
            // note: thows exepction if default owner guid is not provided, and any object has an unset (null) guid
            ConnectionHandler.SaveImportFile(modelObjects, "basicExampleModel.smadsteel", defaultOwnerGuid: ownerGuid);
        }

    }
}
