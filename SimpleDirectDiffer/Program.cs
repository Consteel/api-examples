using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;

using ConSteel.Connection;
using ConSteel.MathObjects;
using ConSteel.ModelObjects;
using ConSteel.ModelObjects.Structural;

namespace SimpleDirectDiffer
{
    static class Program
    {
        public static Guid OwnerGuid { get; private set; } = new Guid("9d324ec5-1113-4f57-be37-17ac6f37a3e2");
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        public static void CalculateMemberChanges(string originalPath, string newPath)
        {
            // load in the two .smadsteel models
            var originalModel = ConnectionHandler.LoadImportFile(originalPath.Replace("\"",""));
            var newModel = ConnectionHandler.LoadImportFile(newPath.Replace("\"", ""));

            var deleted = new Dictionary<Guid, Beam>();
            var added = new Dictionary<Guid, Beam>();
            var changed = new Dictionary<Guid, Beam>();
            var unchanged = new Dictionary<Guid, Beam>();

            // check the changes
            foreach (Beam beam in originalModel[typeof(Beam)])
                deleted.Add(beam.InstanceGuid, beam);
            foreach (Beam newBeam in newModel[typeof(Beam)])
            {
                if (deleted.TryGetValue(newBeam.InstanceGuid, out Beam originalBeam))
                {
                    if (newBeam.Section.InstanceGuid == originalBeam.Section.InstanceGuid
                        && newBeam.Edge.StartNode.Value == originalBeam.Edge.StartNode.Value
                        && newBeam.Edge.EndNode.Value == originalBeam.Edge.EndNode.Value)
                        unchanged.Add(newBeam.InstanceGuid, newBeam);
                    else
                        changed.Add(newBeam.InstanceGuid, newBeam);
                }
                else
                    added.Add(newBeam.InstanceGuid, newBeam);
            }

            // assign beams to new ConSteel layers representing the changes
            var deletedLayer = new Layer("Deleted", true, false, false, OwnerGuid);
            deletedLayer.Style.Color = new Color(255, 255, 0, 0);
            foreach (Beam beam in deleted.Values)
                beam.Layer = deletedLayer;

            var addedLayer = new Layer("Added", true, false, false, OwnerGuid);
            addedLayer.Style.Color = new Color(255, 0, 255, 0);
            foreach (Beam beam in added.Values)
                beam.Layer = addedLayer;

            var changedLayer = new Layer("Changed", true, false, false, OwnerGuid);
            changedLayer.Style.Color = new Color(255, 204, 204, 0);
            foreach (Beam beam in changed.Values)
                beam.Layer = changedLayer;

            var unchangedLayer = new Layer("Unchanged", true, false, false, OwnerGuid);
            unchangedLayer.Style.Color = new Color(100, 100, 100, 100);
            foreach (Beam beam in unchanged.Values)
                beam.Layer = unchangedLayer;

            // re-add deleted beams to the output model
            var outputModel = newModel.SelectMany(kvPair => kvPair.Value)
                .Concat(deleted.Values);

            // save the output model
            var outputModelPath = @"d:\Changes.smadsteel";
            ConnectionHandler.SaveImportFile(outputModel, outputModelPath, null, (o,n) => { });

            // start consteel with the saved out model
            var conSteelPath = @"C:\Program Files\ConSteel 14_929\ConSteel.exe";
            Process.Start($"\"{conSteelPath}\"", $"\"{outputModelPath}\"");
        }
    }
}
