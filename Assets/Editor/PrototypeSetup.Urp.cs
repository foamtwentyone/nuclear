using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Nuclear.EditorTools
{
    /// <summary>
    /// Переход на URP: пункт меню ставит пакет и настраивает конвейер. Код, которому нужны типы URP,
    /// живёт в отдельной сборке Nuclear.Editor.Urp (она собирается, только когда пакет установлен),
    /// поэтому здесь к ней обращаемся через отражение — без пакета проект тоже компилируется.
    /// </summary>
    public static partial class PrototypeSetup
    {
        private const string UrpSetupType = "Nuclear.EditorTools.Urp.UrpSetup, Nuclear.Editor.Urp";

        private static bool UrpSetupAvailable => Type.GetType(UrpSetupType) != null;

        [MenuItem("Nuclear/Включить URP (постобработка)")]
        public static void EnableUrp()
        {
            if (!UrpSetupAvailable)
            {
                Client.Add("com.unity.render-pipelines.universal");
                EditorUtility.DisplayDialog("URP",
                    "Устанавливаю пакет Universal RP. Когда Unity закончит импорт и перекомпиляцию, выберите «Nuclear → Включить URP» ещё раз.",
                    "OK");
                return;
            }

            CallUrp("Install");
            if (EditorUtility.DisplayDialog("URP включён",
                    "Пересобрать сцену сейчас, чтобы материалы и постобработка перешли на URP?", "Пересобрать", "Позже"))
                Build();
        }

        /// <summary>Вызывает метод UrpSetup, если URP установлен и включён; иначе ничего не делает.</summary>
        private static void CallUrp(string method, params object[] arguments)
        {
            var type = Type.GetType(UrpSetupType);
            type?.GetMethod(method, BindingFlags.Public | BindingFlags.Static)?.Invoke(null, arguments);
        }

        private static void ConfigureCameraForPipeline(Camera camera)
        {
            if (UsingUrp)
                CallUrp("ConfigureCamera", camera);
        }
    }
}
