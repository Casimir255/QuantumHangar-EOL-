using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.FileSystem;
using VRage.Game.Models;
using VRage.Utils;
using VRageMath;
using static System.Environment;

namespace QuantumHangar.Utils
{
    public class GridObjExtractor
    {


    }

	/*
    private void Test(List<MyCubeGrid> baseGrids, bool convertModelsFromSBC, bool exportObjAndSBC)
    {
		materialID = 0;
		MyValueFormatter.GetFormatedDateTimeForFilename(DateTime.Now);
		string name = MyUtils.StripInvalidChars(baseGrids[0].DisplayName.Replace(' ', '_'));
		string path = MyFileSystem.UserDataPath;
		string path2 = "ExportedModels";
		if (!convertModelsFromSBC || exportObjAndSBC)
		{
			path = Environment.GetFolderPath((SpecialFolder)0);
			path2 = MyPerGameSettings.GameNameSafe + "_ExportedModels";
		}
		string folder = Path.Combine(path, path2, name);
		int num = 0;
		while (Directory.Exists(folder))
		{
			num++;
			folder = Path.Combine(path, path2, $"{name}_{num:000}");
		}
		MyUtils.CreateFolder(folder);
		if (!convertModelsFromSBC || exportObjAndSBC)
		{
			bool flag = false;
			string prefabPath = Path.Combine(folder, name + ".sbc");
			foreach (MyCubeGrid baseGrid in baseGrids)
			{
				var enumerator2 = baseGrid.CubeBlocks.GetEnumerator();
				try
				{
					while (enumerator2.MoveNext())
					{
						if (!enumerator2.Current.BlockDefinition.Context.IsBaseGame)
						{
							flag = true;
							break;
						}
					}
				}
				finally
				{
					((IDisposable)enumerator2).Dispose();
				}
			}
			if (!flag)
			{
				CreatePrefabFile(baseGrids, name, prefabPath);
				//MyRenderProxy.TakeScreenshot(tumbnailMultiplier, Path.Combine(folder, name + ".png"), debug: false, ignoreSprites: true, showNotification: false);

					//PackFiles(folder, name);
				
			}
			else
			{
				//MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Error, MyMessageBoxButtonsType.OK, new StringBuilder(string.Format(MyTexts.GetString(MyCommonTexts.ExportToObjModded), folder))));
			}
		}
		if (!(exportObjAndSBC || convertModelsFromSBC))
		{
			return;
		}
		List<Vector3> vertices = new List<Vector3>();
		List<TriangleWithMaterial> triangles = new List<TriangleWithMaterial>();
		List<Vector2> uvs = new List<Vector2>();
		List<MyExportModel.Material> materials = new List<MyExportModel.Material>();
		int currVerticesCount = 0;
		try
		{
			GetModelDataFromGrid(baseGrids, vertices, triangles, uvs, materials, currVerticesCount);
			string filename = Path.Combine(folder, name + ".obj");
			string matFilename = Path.Combine(folder, name + ".mtl");
			CreateObjFile(name, filename, matFilename, vertices, triangles, uvs, materials, currVerticesCount);
			List<renderColoredTextureProperties> list = new List<renderColoredTextureProperties>();
			CreateMaterialFile(folder, matFilename, materials, list);
			if (list.Count > 0)
			{
				MyRenderProxy.RenderColoredTextures(list);
			}
			MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Info, MyMessageBoxButtonsType.NONE_TIMEOUT, new StringBuilder(string.Format(MyTexts.GetString(MyCommonTexts.ExportToObjComplete), folder)), null, null, null, null, null, delegate
			{
				ConvertNextGrid(placeOnly: false);
			}, 1000));
		}
		catch (Exception ex)
		{
			MySandboxGame.Log.WriteLine("Error while exporting to obj file.");
			MySandboxGame.Log.WriteLine(ex.ToString());
			MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Error, MyMessageBoxButtonsType.OK, new StringBuilder(string.Format(MyTexts.GetString(MyCommonTexts.ExportToObjFailed), folder))));
		}
	}

	private struct TriangleWithMaterial
	{
		MyTriangleVertexIndices triangle;

		MyTriangleVertexIndices uvIndices;

		string material;
	}
	*/
}
