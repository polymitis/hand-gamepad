PROJECTNAME=Morpheus
UNITY_EDITOR_VERSION=2019.2.19f1
UNITY_EDITOR_ROOT=/Applications/Unity/Hub/Editor/$(UNITY_EDITOR_VERSION)
UNITY_APP=$(UNITY_EDITOR_ROOT)/Unity.app/Contents/MacOS/Unity
UNITY_PROJ_ROOT=$(CURDIR)
UNITY_PROJ_DIRS=$(UNITY_PROJ_ROOT)/Assets $(UNITY_PROJ_ROOT)/Packages $(UNITY_PROJ_ROOT)/ProjectSettings

all: release development

release: ios-rel

development: ios-dev

ios-rel: unity-ios-rel
	xcodebuild -project "$(UNITY_PROJ_ROOT)/Builds/ios/release/Unity-iPhone.xcodeproj" \
		-scheme Unity-iPhone -configuration Release -allowProvisioningUpdates \
		build

unity-ios-rel: $(UNITY_PROJ_DIRS)
	$(UNITY_APP) -batchmode -quit \
		-projectPath "$(UNITY_PROJ_ROOT)" \
		-logFile /dev/stdout \
		-executeMethod Nordeus.Build.CommandLineBuild.Build \
			-out "$(UNITY_PROJ_ROOT)/Builds/ios/release" \
			-target iOS \
			-buildNumber 0 \
			-buildVersion 0.1

ios-dev: unity-ios-dev
	xcodebuild -project "$(UNITY_PROJ_ROOT)/Builds/ios/development/Unity-iPhone.xcodeproj" \
		-scheme Unity-iPhone -configuration Debug -allowProvisioningUpdates \
		build

unity-ios-dev: $(UNITY_PROJ_DIRS)
	$(UNITY_APP) -batchmode -quit \
		-projectPath "$(UNITY_PROJ_ROOT)" \
		-logFile /dev/stdout \
		-executeMethod Nordeus.Build.CommandLineBuild.Build \
			-out "$(UNITY_PROJ_ROOT)/Builds/ios/development" \
			-target iOS \
			-buildNumber 0 \
			-buildVersion 0.1 \
			-development 1

clean: clean-ios

clean-ios: clean-ios-rel clean-ios-dev

clean-ios-rel:
	rm -Rf "$(UNITY_PROJ_ROOT)/Builds/ios/release"

clean-ios-dev:
	rm -Rf "$(UNITY_PROJ_ROOT)/Builds/ios/development"
