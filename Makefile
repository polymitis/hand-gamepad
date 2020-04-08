PROJECTNAME=Morpheus

# Unity
UNITY_PROJ_ROOT=$(CURDIR)/unity
UNITY_EDITOR_VERSION=2019.2.19f1
UNITY_EDITOR_ROOT=/Applications/Unity/Hub/Editor/$(UNITY_EDITOR_VERSION)
UNITY_APP=$(UNITY_EDITOR_ROOT)/Unity.app/Contents/MacOS/Unity
UNITY_PROJ_DIRS=$(UNITY_PROJ_ROOT)/Assets $(UNITY_PROJ_ROOT)/Packages $(UNITY_PROJ_ROOT)/ProjectSettings
UNITY_PLUGINS_IOS_DIR=Plugins/iOS

# MediaPipe
MP_PROJ_ROOT=$(CURDIR)/mediapipe
MP_WS_ROOT=$(MP_PROJ_ROOT)/mediapipe/workspace
MP_WS_BUILD_ROOT=mediapipe/workspace
BAZEL_110=$(SCRATCHHOME)/bazel-1.1.0/output/bazel

# Charades plugin
MP_CHARADES_ROOT=$(MP_WS_ROOT)/charades
MP_CHARADES_BUILD_ROOT = $(MP_WS_BUILD_ROOT)/charades
UNITY_CHARADES_ROOT=$(UNITY_PROJ_ROOT)/Assets/Plugins/Charades

# Helpers
.PHONY: list
list:
	@$(MAKE) -pRrq -f $(lastword $(MAKEFILE_LIST)) : 2>/dev/null | \
		awk -v RS= -F: '/^# File/,/^# Finished Make data base/ {if ($$1 !~ "^[#.]") {print $$1}}' | \
		sort | egrep -v -e '^[^[:alnum:]]' -e '^$@$$'

# Targets
ios-rel: mp-charades-ios unity-ios-rel unity-xcode-ios-rel

ios-dev: mp-charades-ios unity-ios-dev unity-xcode-ios-dev

unity-xcode-ios-rel:
	cp -f provisioning_profile.mobileprovision "$(HOME)/Library/MobileDevice/Provisioning Profiles/$(UNITY_IOS_UUID).mobileprovision" && \
	xcodebuild -project "$(UNITY_PROJ_ROOT)/Builds/ios/release/Unity-iPhone.xcodeproj" \
		-scheme Unity-iPhone -configuration Release \
		PROVISIONING_PROFILE=$(UNITY_IOS_UUID) \
		CODE_SIGN_IDENTITY="$(UNITY_IOS_ID)" \
		-allowProvisioningUpdates \
		build

unity-xcode-ios-dev:
	cp -f provisioning_profile.mobileprovision "$(HOME)/Library/MobileDevice/Provisioning Profiles/$(UNITY_IOS_UUID).mobileprovision" && \
	xcodebuild -project "$(UNITY_PROJ_ROOT)/Builds/ios/development/Unity-iPhone.xcodeproj" \
		-scheme Unity-iPhone -configuration Debug \
		PROVISIONING_PROFILE=$(UNITY_IOS_UUID) \
		CODE_SIGN_IDENTITY="$(UNITY_IOS_ID)" \
		-allowProvisioningUpdates \
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

mp-charades-ios:
	cd $(MP_PROJ_ROOT) && echo "Entering mediapipe/ directory" && \
	$(BAZEL_110) build --config=ios_arm64 $(MP_CHARADES_BUILD_ROOT)/ios:Charades && \
	rm -Rf $(UNITY_CHARADES_ROOT)/$(UNITY_PLUGINS_IOS_DIR)/Native/Charades.framework && \
	unzip bazel-bin/$(MP_CHARADES_BUILD_ROOT)/ios/Charades.zip -d $(UNITY_CHARADES_ROOT)/$(UNITY_PLUGINS_IOS_DIR)/Native && \
	cd .. && echo "Leaving mediapipe/ directory"

clean: clean-ios

clean-ios: clean-unity-ios-rel clean-unity-ios-dev clean-mp-charades-ios

clean-unity-ios-rel:
	rm -Rf "$(UNITY_PROJ_ROOT)/Builds/ios/release"

clean-unity-ios-dev:
	rm -Rf "$(UNITY_PROJ_ROOT)/Builds/ios/development"

clean-mp-charades-ios:
	cd $(MP_PROJ_ROOT) && echo "Entering mediapipe/ directory" && \
        $(BAZEL_110) clean && \
        cd .. && echo "Leaving mediapipe/ directory" && \
	rm -Rf $(UNITY_CHARADES_ROOT)/$(UNITY_PLUGINS_IOS_DIR)/Native/Charades.framework
