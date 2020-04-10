// MIT License
//
// Copyright (c) 2020 Petros Fountas
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

public class CharadesBuildPostProc
{
	[PostProcessBuildAttribute(1)]
	public static void OnPostProcessBuild(BuildTarget target, string path)
	{
		if (target == BuildTarget.iOS)
		{
			PBXProject project = new PBXProject();
			string sPath = PBXProject.GetPBXProjectPath(path);
			project.ReadFromFile(sPath);

		    string f = project.GetUnityFrameworkTargetGuid();

			project.AddBuildProperty(f,
				"ENABLE_BITCODE",
				"NO");

			string a = project.GetUnityMainTargetGuid();

			project.AddFrameworkToProject(a, "Accelerate.framework", false);

			string c = project.FindFileGuidByProjectPath(
				"Frameworks/Plugins/Charades/Plugins/iOS/Native/Charades.framework");

			project.AddFileToEmbedFrameworks(a, c);

			project.AddBuildProperty(a,
				"ENABLE_BITCODE",
				"NO");

			// modify frameworks and settings as desired
			File.WriteAllText(sPath, project.WriteToString());
		}
	}
}
