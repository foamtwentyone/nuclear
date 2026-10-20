using System.Collections.Generic;
using Nuclear.Enemies;
using Nuclear.EditorTools.LowPoly;
using Nuclear.Items;
using Nuclear.Level;
using Nuclear.Network;
using Nuclear.Player;
using Nuclear.Visuals;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace Nuclear.EditorTools
{
    /// <summary>Префабы: игрок, предметы, Слепыш, дверь бункера.</summary>
    public static partial class PrototypeSetup
    {
        private static GameObject BuildPlayerPrefab(GearPrefabs gear)
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<NetworkObject>();

            var netTransform = root.AddComponent<OwnerNetworkTransform>();
            // Игрок поворачивается только вокруг вертикали, масштаб не меняется.
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;

            root.AddComponent<PlayerStamina>();
            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerCarry>();
            root.AddComponent<PlayerSurvival>();
            root.AddComponent<Dosimeter>();
            root.AddComponent<PlayerNoise>();
            root.AddComponent<PlayerVoice>();
            root.AddComponent<PlayerAudio>();
            root.AddComponent<ScreenEffects>();
            root.AddComponent<PlayerGhost>();
            Configure(root.AddComponent<PlayerGear>(), new Dictionary<string, object>
            {
                ["bandagePrefab"] = gear.Bandage,
                ["flashlightPrefab"] = gear.Flashlight,
                ["crowbarPrefab"] = gear.Crowbar,
                ["batteryPrefab"] = gear.Battery,
                ["filterPrefab"] = gear.Filter,
                ["canPrefab"] = gear.Can,
            });
            root.AddComponent<PlayerPry>();
            root.AddComponent<PlayerHud>();

            // Тело с опорной точкой в тазу: так его удобно уронить при смерти (см. PlayerSurvival).
            var body = new MeshKit();
            Models.Survivor(body);
            var bodyModel = AddModel(root.transform, "Body", body, new Vector3(0f, 0.9f, 0f), Quaternion.identity);
            // Предмет в правой руке — его видят другие игроки (PlayerGear включает нужную модель).
            AddHeldModels(bodyModel.transform, "Hand", Models.SurvivorHand, firstPerson: false);

            // Огонёк призрака: виден другим, когда игрок погиб (см. PlayerGhost).
            var wisp = new MeshKit();
            var wispRandom = new System.Random(5);
            wisp.Ellipsoid(Vector3.zero, Quaternion.identity, new Vector3(0.22f, 0.26f, 0.22f), 8, 12, new[] { Swatch.Teeth }, wispRandom);
            wisp.Ellipsoid(new Vector3(0f, -0.3f, 0f), Quaternion.identity, new Vector3(0.1f, 0.18f, 0.1f), 6, 10, new[] { Swatch.Teeth }, wispRandom);
            var ghostObject = AddModel(root.transform, "Ghost", wisp, new Vector3(0f, 1.6f, 0f), Quaternion.identity, ghostMaterial);
            var ghostRenderer = ghostObject.GetComponent<MeshRenderer>();
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var ghostLight = ghostObject.AddComponent<Light>();
            ghostLight.type = LightType.Point;
            ghostLight.color = new Color(0.6f, 0.8f, 1f);
            ghostLight.intensity = 0.6f;
            ghostLight.range = 3f;

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.fieldOfView = 75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
            cameraObject.AddComponent<AudioListener>();
            // Предмет в руке от первого лица — справа внизу кадра, видит только сам игрок.
            AddHeldModels(cameraObject.transform, "Viewmodel", new Vector3(0.24f, -0.22f, 0.42f), firstPerson: true);

            // Фонарь светит из головы туда, куда смотрит игрок (включает PlayerGear).
            var flashlight = new GameObject("Flashlight").AddComponent<Light>();
            flashlight.transform.SetParent(cameraObject.transform, false);
            flashlight.transform.localPosition = new Vector3(0.15f, -0.15f, 0.1f);
            flashlight.type = LightType.Spot;
            flashlight.spotAngle = 55f;
            flashlight.innerSpotAngle = 25f;
            flashlight.range = 22f;
            flashlight.intensity = 3f;
            flashlight.color = new Color(1f, 0.93f, 0.78f);
            flashlight.shadows = LightShadows.Soft;
            flashlight.enabled = false;
            ConfigureCameraForPipeline(camera);

            AddAsh(root.transform);

            return SavePrefab(root, "Player");
        }

        /// <summary>
        /// Модели всех мелких предметов в одной точке (рука или кадр от первого лица), все выключены.
        /// Имя модели — имя GearKind: по нему PlayerGear включает предмет из выбранной ячейки.
        /// </summary>
        private static void AddHeldModels(Transform parent, string name, Vector3 position, bool firstPerson)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            foreach (GearKind kind in System.Enum.GetValues(typeof(GearKind)))
            {
                var kit = new MeshKit();
                HeldModel(kind)(kit);
                var model = AddModel(holder.transform, kind.ToString(), kit, Vector3.zero, HeldRotation(kind, firstPerson));
                if (firstPerson)
                {
                    // Предмет у камеры не должен отбрасывать тень на мир.
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                model.SetActive(false);
            }
        }

        private static System.Action<MeshKit> HeldModel(GearKind kind) => kind switch
        {
            GearKind.Bandage => Models.Bandage,
            GearKind.Flashlight => Models.Flashlight,
            GearKind.Crowbar => Models.Crowbar,
            GearKind.FlashlightBattery => Models.FlashlightBattery,
            GearKind.Filter => Models.Filter,
            _ => Models.Can,
        };

        /// <summary>Как предмет лежит в руке: фонарь светит вперёд, лом поднят наискось, мелочь чуть повёрнута к камере.</summary>
        private static Quaternion HeldRotation(GearKind kind, bool firstPerson) => kind switch
        {
            GearKind.Flashlight => Quaternion.identity,
            GearKind.Crowbar => Quaternion.FromToRotation(Vector3.right, firstPerson ? new Vector3(-0.35f, 0.8f, 0.45f) : new Vector3(0f, 0.85f, 0.5f)),
            GearKind.Bandage or GearKind.FlashlightBattery => Quaternion.Euler(10f, -30f, 0f),
            GearKind.Filter => Quaternion.Euler(15f, 0f, 10f),
            _ => Quaternion.Euler(10f, 0f, 0f),
        };

        /// <summary>Падающий пепел вокруг игрока. Эмиттер следует за игроком, хлопья остаются в мире.</summary>
        private static void AddAsh(Transform parent)
        {
            var ashObject = new GameObject("Ash");
            ashObject.transform.SetParent(parent, false);
            ashObject.transform.localPosition = new Vector3(0f, 9f, 0f);

            var ash = ashObject.AddComponent<ParticleSystem>();
            ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ash.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = 16f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new Color(0.82f, 0.8f, 0.77f, 0.85f);
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ash.emission;
            emission.rateOverTime = 180f;

            var shape = ash.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 40f);

            var velocity = ash.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.9f, -0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var noise = ash.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.2f;

            ashObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial();
        }

        private static GameObject BuildBatteryPrefab()
        {
            var root = CreateItemRoot("Battery", mass: 25f, continuous: false);
            var kit = new MeshKit();
            Models.Battery(kit);
            AddModel(root.transform, "Battery", kit, Vector3.zero, Quaternion.identity)
                .AddComponent<BoxCollider>().size = new Vector3(0.6f, 0.4f, 0.4f);

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "Indicator";
            Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.transform.SetParent(root.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.08f, -0.205f);
            indicator.transform.localScale = new Vector3(0.12f, 0.05f, 0.02f);
            indicator.GetComponent<Renderer>().sharedMaterial = SaveMaterial("Indicator", Color.green, emission: Color.green);

            AddSparks(root, new Vector3(0f, 0.25f, 0f));

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Аккумулятор",
                ["maxCarriers"] = 2,
                ["holdDistance"] = 1.3f,
                ["soloHoldDrop"] = 0.7f,
                ["soloSpeedMultiplier"] = 0.45f,
                ["sharedSpeedMultiplier"] = 0.85f,
                ["canThrow"] = false,
                ["impactSound"] = PhysicsItem.ImpactSound.Heavy,
                ["impactNoiseRadius"] = 15f,
            });
            root.AddComponent<Battery>();

            return SavePrefab(root, "Battery");
        }

        private static GameObject BuildCratePrefab()
        {
            var root = CreateItemRoot("Crate", mass: 6f, continuous: false);
            var kit = new MeshKit();
            Models.Crate(kit, 0.6f);
            AddModel(root.transform, "Crate", kit, Vector3.zero, Quaternion.identity)
                .AddComponent<BoxCollider>().size = Vector3.one * 0.6f;

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Ящик",
                ["soloSpeedMultiplier"] = 0.8f,
                ["throwImpulse"] = 18f,
                ["impactSound"] = PhysicsItem.ImpactSound.Wood,
                ["impactNoiseRadius"] = 10f,
            });

            return SavePrefab(root, "Crate");
        }

        private static GameObject BuildCanPrefab()
        {
            var root = CreateItemRoot("Can", mass: 0.3f, continuous: true);
            var kit = new MeshKit();
            Models.Can(kit);
            var capsule = AddModel(root.transform, "Can", kit, Vector3.zero, Quaternion.identity).AddComponent<CapsuleCollider>();
            capsule.radius = 0.047f;
            capsule.height = 0.124f;

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Консерва",
                ["holdDistance"] = 1f,
                ["throwImpulse"] = 3.5f,
                ["impactSound"] = PhysicsItem.ImpactSound.Tin,
                ["impactNoiseRadius"] = 12f,
            });
            Configure(root.AddComponent<GearPickup>(), new Dictionary<string, object> { ["kind"] = GearKind.Can });

            return SavePrefab(root, "Can");
        }

        /// <summary>
        /// Капот одной из машин с аккумулятором: ставится поверх статичной машины (Models.Car с openableHood).
        /// Петли — у лобового стекла, крышка поднимается вверх. Триггер — чтобы игрок мог «посмотреть» на капот.
        /// </summary>
        private static GameObject BuildCarHoodPrefab(Swatch paint, int seed, ItemPrefabs items, GearPrefabs gear)
        {
            var root = new GameObject($"Car Hood {seed - 200}");
            root.AddComponent<NetworkObject>();

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.9f, 1.48f);
            trigger.size = new Vector3(1.8f, 0.35f, 1.3f);

            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(0f, 0.9f, 0.86f);
            var kit = new MeshKit();
            Models.CarHoodPanel(kit, new System.Random(seed), paint);
            AddModel(hinge.transform, "Panel", kit, Vector3.zero, Quaternion.identity);

            Configure(root.AddComponent<CarHood>(), new Dictionary<string, object>
            {
                ["batteryPrefab"] = items.Battery,
                ["filterPrefab"] = items.Filter,
                ["bandagePrefab"] = gear.Bandage,
                ["cellPrefab"] = gear.Battery,
            });

            return SavePrefab(root, $"Car Hood {seed - 200}");
        }

        /// <summary>Снаряжение для кармана: маленький физический предмет с GearPickup.</summary>
        private static GameObject BuildGearPrefab(string name, string displayName, GearKind kind, System.Action<MeshKit> model, Vector3 colliderSize, float mass)
        {
            var root = CreateItemRoot(name, mass, continuous: true);
            var kit = new MeshKit();
            model(kit);
            AddModel(root.transform, name, kit, Vector3.zero, Quaternion.identity)
                .AddComponent<BoxCollider>().size = colliderSize;

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = displayName,
                ["holdDistance"] = 1f,
                ["throwImpulse"] = kind == GearKind.Crowbar ? 6f : 3f,
                ["impactSound"] = kind == GearKind.Crowbar ? PhysicsItem.ImpactSound.Heavy : PhysicsItem.ImpactSound.Tin,
                ["impactNoiseRadius"] = kind == GearKind.Crowbar ? 12f : 6f,
            });
            Configure(root.AddComponent<GearPickup>(), new Dictionary<string, object> { ["kind"] = kind });

            return SavePrefab(root, name);
        }

        private static GameObject BuildFilterPrefab()
        {
            var root = CreateItemRoot("Filter", mass: 0.4f, continuous: true);
            var kit = new MeshKit();
            Models.Filter(kit);
            var capsule = AddModel(root.transform, "Filter", kit, Vector3.zero, Quaternion.identity).AddComponent<CapsuleCollider>();
            capsule.radius = 0.063f;
            capsule.height = 0.13f;

            var item = root.AddComponent<PhysicsItem>();
            Configure(item, new Dictionary<string, object>
            {
                ["displayName"] = "Фильтр",
                ["holdDistance"] = 1f,
                ["throwImpulse"] = 3.5f,
                ["impactSound"] = PhysicsItem.ImpactSound.Tin,
                ["impactNoiseRadius"] = 8f,
            });
            Configure(root.AddComponent<GearPickup>(), new Dictionary<string, object> { ["kind"] = GearKind.Filter });

            return SavePrefab(root, "Filter");
        }

        /// <summary>
        /// Подвижная часть входа в Бункер-12: створка, щиток, лампа и логика конца уровня.
        /// Стены здания — статичная часть уровня (BuildBunker12), чтобы попасть в навигационную сетку.
        /// </summary>
        private static GameObject BuildDoorPrefab()
        {
            var root = new GameObject("Bunker12 Door");
            root.AddComponent<NetworkObject>();
            var random = new System.Random(12);

            var panelKit = new MeshKit();
            Models.DoorPanel(panelKit, random);
            var panel = AddModel(root.transform, "Panel", panelKit, new Vector3(0f, 1.3f, 0f), Quaternion.identity);
            panel.AddComponent<BoxCollider>().size = new Vector3(3f, 2.6f, 0.3f);
            // Слепыш ходит по сетке и сквозь закрытую створку не пойдёт.
            var obstacle = panel.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(3f, 2.6f, 0.3f);
            obstacle.carving = true;

            var socketKit = new MeshKit();
            Models.Socket(socketKit);
            AddModel(root.transform, "Socket", socketKit, new Vector3(2.4f, 1.2f, -0.35f), Quaternion.identity);

            var batteryKit = new MeshKit();
            Models.Battery(batteryKit);
            var inserted = AddModel(root.transform, "Inserted Battery", batteryKit, new Vector3(2.4f, 1.12f, -0.52f), Quaternion.identity, scale: 0.6f);
            inserted.SetActive(false);

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Lamp";
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 3.75f, -0.3f);
            lamp.transform.localScale = Vector3.one * 0.22f;
            lamp.GetComponent<Renderer>().sharedMaterial = SaveMaterial("Lamp", Color.red, emission: Color.red * 2f);

            var glow = new GameObject("Light").AddComponent<Light>();
            glow.transform.SetParent(lamp.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0f, -2f);
            glow.type = LightType.Point;
            glow.range = 7f;
            glow.intensity = 2f;
            glow.color = Color.red;

            root.AddComponent<BunkerDoor>();
            root.AddComponent<LevelOutcome>();

            return SavePrefab(root, "Bunker12Door");
        }

        private static GameObject BuildBlindOnePrefab()
        {
            var root = new GameObject("BlindOne");

            root.AddComponent<Rigidbody>().isKinematic = true;
            // Длинное круглое тело: капсула вдоль Z.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0.85f, 0.1f);
            capsule.height = 3.3f;
            capsule.radius = 0.8f;

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;

            // Туша вылеплена целиком; нос — дочерний объект тела (сжимается вместе с ним), лапы — отдельно.
            var bodyKit = new MeshKit();
            Models.BlindOneBody(bodyKit);
            var body = AddModel(root.transform, "Body", bodyKit, Vector3.zero, Quaternion.identity);

            var noseKit = new MeshKit();
            Models.BlindOneNoseStar(noseKit);
            AddModel(body.transform, "Nose", noseKit, Models.BlindOneNose, Quaternion.identity);

            // Порядок лап: передняя левая, передняя правая, задняя левая, задняя правая.
            for (int i = 0; i < Models.BlindOneHips.Length; i++)
            {
                var legKit = new MeshKit();
                Models.BlindOneLeg(legKit);
                AddModel(root.transform, $"Leg {i}", legKit, Models.BlindOneHips[i], Quaternion.identity);
            }

            root.AddComponent<BlindOne>();
            root.AddComponent<BlindOneAnimator>();

            return SavePrefab(root, "BlindOne");
        }

        /// <summary>Пустой корень предмета с физикой и сетью; модели и коллайдеры — дочерние.</summary>
        private static GameObject CreateItemRoot(string name, float mass, bool continuous)
        {
            var root = new GameObject(name);

            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (continuous)
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;
            root.AddComponent<NetworkRigidbody>();

            return root;
        }

        private static void AddSparks(GameObject root, Vector3 position)
        {
            var sparksObject = new GameObject("Sparks");
            sparksObject.transform.SetParent(root.transform, false);
            sparksObject.transform.localPosition = position;

            var sparks = sparksObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = 0.04f;
            main.startColor = new Color(1f, 0.85f, 0.4f);
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            sparksObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMaterial();
        }
    }
}
