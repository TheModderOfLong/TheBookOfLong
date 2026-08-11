using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 在开始界面主菜单中增加“模组管理”按钮，用于打开《龙之书 Mod》管理界面。
    /// </summary>
    [HarmonyPatch(typeof(GameTitleController), "ShowMainMenu")]
    public static class TitleModManagerButtonPatch
    {
        private const string ButtonName = "TheExtensionOfLong_ModManagerButton";
        private const string ButtonText = "模组管理";
        private const float FallbackButtonStep = 120f;

        [HarmonyPrefix]
        public static void Prefix(GameTitleController __instance)
        {
            try
            {
                EnsureButton(__instance);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("TitleModManagerButtonPatch: 创建开始界面模组管理按钮失败 - " + ex.Message);
            }
        }

        private static void EnsureButton(GameTitleController titleController)
        {
            if (titleController == null || titleController.MainMenu == null)
                return;

            Transform mainMenu = titleController.MainMenu.transform;
            if (mainMenu == null || mainMenu.Find(ButtonName) != null)
                return;

            GameObject template = GetButtonTemplate(titleController);
            if (template == null)
            {
                LoggerManager.Warning("TitleModManagerButtonPatch: 未找到可复用的开始界面按钮模板");
                return;
            }

            Vector3 targetPosition = GetTargetPosition(titleController);
            ShiftButtonsAboveSetting(titleController);

            GameObject buttonObject = Object.Instantiate(template).TryCast<GameObject>();
            if (buttonObject == null)
            {
                LoggerManager.Warning("TitleModManagerButtonPatch: 复制开始界面按钮失败");
                return;
            }

            buttonObject.name = ButtonName;
            buttonObject.transform.SetParent(mainMenu, false);
            buttonObject.transform.localPosition = targetPosition;
            buttonObject.transform.localRotation = template.transform.localRotation;
            buttonObject.transform.localScale = template.transform.localScale;
            buttonObject.SetActive(true);

            SetSiblingIndexNearSetting(titleController, buttonObject);
            SetButtonText(buttonObject, ButtonText);
            BindButtonClick(buttonObject);
        }

        private static GameObject GetButtonTemplate(GameTitleController titleController)
        {
            if (titleController.settingButton != null) return titleController.settingButton;
            if (titleController.loadButton != null) return titleController.loadButton;
            if (titleController.startButton != null) return titleController.startButton;
            if (titleController.continueButton != null) return titleController.continueButton;
            return titleController.quitButton;
        }

        private static Vector3 GetTargetPosition(GameTitleController titleController)
        {
            if (titleController.loadButton != null)
                return titleController.loadButton.transform.localPosition;

            if (titleController.settingButton != null && titleController.quitButton != null)
            {
                Vector3 settingPosition = titleController.settingButton.transform.localPosition;
                float step = GetButtonStep(titleController);
                return new Vector3(settingPosition.x, settingPosition.y + step, settingPosition.z);
            }

            return Vector3.zero;
        }

        private static void ShiftButtonsAboveSetting(GameTitleController titleController)
        {
            float step = GetButtonStep(titleController);
            ShiftButton(titleController.continueButton, step);
            ShiftButton(titleController.startButton, step);
            ShiftButton(titleController.loadButton, step);
        }

        private static float GetButtonStep(GameTitleController titleController)
        {
            if (titleController.loadButton != null && titleController.settingButton != null)
            {
                float step = titleController.loadButton.transform.localPosition.y
                    - titleController.settingButton.transform.localPosition.y;
                if (Math.Abs(step) > 0.01f)
                    return step;
            }

            if (titleController.settingButton != null && titleController.quitButton != null)
            {
                float step = titleController.settingButton.transform.localPosition.y
                    - titleController.quitButton.transform.localPosition.y;
                if (Math.Abs(step) > 0.01f)
                    return step;
            }

            return FallbackButtonStep;
        }

        private static void ShiftButton(GameObject button, float step)
        {
            if (button == null)
                return;

            Transform transform = button.transform;
            Vector3 position = transform.localPosition;
            transform.localPosition = new Vector3(position.x, position.y + step, position.z);
        }

        private static void SetButtonText(GameObject buttonObject, string text)
        {
            SetSimpleDetailTexts(buttonObject, text);
            SetUiTexts(buttonObject, text);
            SetTmpTexts(buttonObject, text);
        }

        private static void SetSimpleDetailTexts(GameObject buttonObject, string text)
        {
            var comps = buttonObject.GetComponentsInChildren(Il2CppType.Of<SimpleDetailText>());
            if (comps == null)
                return;

            foreach (var comp in comps)
            {
                SimpleDetailText simpleText = comp == null ? null : comp.TryCast<SimpleDetailText>();
                if (simpleText != null)
                    simpleText.text = text;
            }
        }

        private static void SetUiTexts(GameObject buttonObject, string text)
        {
            var comps = buttonObject.GetComponentsInChildren(Il2CppType.Of<Text>());
            if (comps == null)
                return;

            foreach (var comp in comps)
            {
                Text uiText = comp == null ? null : comp.TryCast<Text>();
                if (uiText != null)
                    uiText.text = text;
            }
        }

        private static void SetTmpTexts(GameObject buttonObject, string text)
        {
            var comps = buttonObject.GetComponentsInChildren(Il2CppType.Of<TMP_Text>());
            if (comps == null)
                return;

            foreach (var comp in comps)
            {
                TMP_Text tmpText = comp == null ? null : comp.TryCast<TMP_Text>();
                if (tmpText != null)
                    tmpText.text = text;
            }
        }

        private static void BindButtonClick(GameObject buttonObject)
        {
            Button button = buttonObject.GetComponent(Il2CppType.Of<Button>())?.TryCast<Button>();
            if (button == null)
            {
                LoggerManager.Warning("TitleModManagerButtonPatch: 新按钮缺少 Button 组件");
                return;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((Action)(() => ModLoadConfigWindow.Open()));
        }

        private static void SetSiblingIndexNearSetting(GameTitleController titleController, GameObject buttonObject)
        {
            if (titleController.settingButton == null || buttonObject == null)
                return;

            int settingIndex = titleController.settingButton.transform.GetSiblingIndex();
            buttonObject.transform.SetSiblingIndex(settingIndex);
        }
    }
}
