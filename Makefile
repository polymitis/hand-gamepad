# Unity
UNITY_PROJ_ROOT=$(CURDIR)/unity
UNITY_EDITOR_VERSION=2020.3.19f1
UNITY_EDITOR_ROOT=/Applications/Unity/Hub/Editor/$(UNITY_EDITOR_VERSION)
UNITY_APP=$(UNITY_EDITOR_ROOT)/Unity.app/Contents/MacOS/Unity
UNITY_PROJ_DIRS=$(UNITY_PROJ_ROOT)/Assets $(UNITY_PROJ_ROOT)/Packages $(UNITY_PROJ_ROOT)/ProjectSettings
UNITY_PLUGINS_IOS_NATIVE_DIR=Plugins/iOS/Native

# MediaPipe
MP_PROJ_ROOT=$(CURDIR)/mediapipe
MP_WS_ROOT=$(MP_PROJ_ROOT)/mediapipe/workspace
MP_WS_BUILD_ROOT=mediapipe/workspace

# Hand gesture detector plugin
HGD_NAME=HandGestureDetector
MP_HGD_PROJ_NAME=mp-hand-gesture-detector
MP_HGD_PROJ_ROOT=$(MP_WS_ROOT)/$(MP_HGD_PROJ_NAME)
MP_HGD_PROJ_BUILD_ROOT = $(MP_WS_BUILD_ROOT)/$(MP_HGD_PROJ_NAME)
UNITY_HGD_PLUGIN_ROOT=$(UNITY_PROJ_ROOT)/Assets/Plugins/$(HGD_NAME)

# Helpers
.PHONY: list
list:
	@$(MAKE) -pRrq -f $(lastword $(MAKEFILE_LIST)) : 2>/dev/null | \
		awk -v RS= -F: '/^# File/,/^# Finished Make data base/ {if ($$1 !~ "^[#.]") {print $$1}}' | \
		sort | egrep -v -e '^[^[:alnum:]]' -e '^$@$$'

# Targets
ios-rel: mp-hgd-ios unity-ios-rel unity-xcode-ios-rel

ios-dev: mp-hgd-ios unity-ios-dev unity-xcode-ios-dev

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

mp-hgd-ios:
	cd $(MP_PROJ_ROOT) && echo "Entering mediapipe/ directory" && \
	bazelisk build -c opt --config=ios_arm64 --incompatible_run_shell_command_string=false --incompatible_objc_provider_remove_compile_info=false $(MP_HGD_PROJ_BUILD_ROOT)/ios:$(HGD_NAME) && \
	rm -Rf $(UNITY_HGD_PLUGIN_ROOT)/$(UNITY_PLUGINS_IOS_NATIVE_DIR)/$(HGD_NAME).framework && \
	unzip bazel-bin/$(MP_HGD_PROJ_BUILD_ROOT)/ios/$(HGD_NAME).zip -d $(UNITY_HGD_PLUGIN_ROOT)/$(UNITY_PLUGINS_IOS_NATIVE_DIR) && \
	cd .. && echo "Leaving mediapipe/ directory"

clean: clean-ios clean-unity clean-bazel

clean-ios: clean-unity-ios-rel clean-unity-ios-dev clean-mp-hgd-ios

clean-unity: clean-unity-ios-rel clean-unity-ios-dev
	rm -Rf Builds/ Library/ temp/ obj/

clean-bazel: clean-mp-hgd-ios
	cd $(MP_PROJ_ROOT) && echo "Entering mediapipe/ directory" && \
	bazel clean --expunge && \
	sudo xcode-select -s /Applications/Xcode.app/Contents/Developer && \
	sudo xcodebuild -license && \
	bazel clean --expunge && \
	cd .. && echo "Leaving mediapipe/ directory"

clean-unity-ios-rel:
	rm -Rf "$(UNITY_PROJ_ROOT)/Builds/ios/release"

clean-unity-ios-dev:
	rm -Rf "$(UNITY_PROJ_ROOT)/Builds/ios/development"

clean-mp-hgd-ios:
	rm -Rf $(UNITY_HGD_PLUGIN_ROOT)/$(UNITY_PLUGINS_IOS_NATIVE_DIR)/$(HGD_NAME).framework
