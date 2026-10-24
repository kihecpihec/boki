using UnityEditor;
using UnityEngine;

namespace Boki.Editor
{
    public static class MariborProductionTools
    {
        internal static readonly MapProductionTools Delivery = new MapProductionTools("Maribor", "maribor", new Color(0.23f, 0.105f, 0.095f));

        [MenuItem("Boki/Maribor Production/Import and Build Preview")]
        public static void ImportAndBuild() => Delivery.ImportAndBuild();

        [MenuItem("Boki/Maribor Production/Open Preview")]
        public static void OpenPreview() => Delivery.OpenPreview();

        [MenuItem("Boki/Maribor Production/Import and Validate Delivery")]
        public static void ImportAndValidate() => Delivery.ImportAndValidate();

        [MenuItem("Boki/Maribor Production/Export Environment Package")]
        public static void ExportPackage() => Delivery.ExportPackage();

        [MenuItem("Boki/Maribor Production/Import, Validate and Package")]
        public static void BuildDelivery() => Delivery.BuildDelivery();

        [MenuItem("Boki/Maribor Production/Capture Review Views")]
        public static void Capture() => Delivery.Capture();

        public static void CaptureAndExport() => Delivery.CaptureAndExport();
    }
}
