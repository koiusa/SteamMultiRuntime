# KeyConfig Architecture

この文書をKeyconfigのクラス構成図と公開インターフェース構成図の正本とします。利用方法、導入、テスト手順は[Keyconfig.md](Keyconfig.md)、公開API再編の履歴は[KeyConfigPublicApiMigration.md](KeyConfigPublicApiMigration.md)を参照してください。

## クラス構成

```mermaid
classDiagram
    class KeyConfigPanel {
        <<public MonoBehaviour>>
        +Open()
        +Close()
    }
    class KeyConfigController {
        <<public facade>>
        +StartRebind(bindingId, bindingGroup)
        +CancelRebind()
        +ExportOverrides()
        +ImportOverrides(json)
    }
    class InputBindingService {
        <<internal>>
        +GetBindingEntries()
        +TryFindConflictingBinding()
    }
    class InputRebindController {
        <<internal>>
        -ExcludePhysicalControlAliases()
        +StartRebind(actionId, bindingIndex, bindingGroup)
    }
    class RebindAliasSuppression {
        <<internal>>
        +BeginPartObservation()
        +ApplyExclusions(operation)
    }
    class KeyConfigView {
        <<internal UI facade>>
    }
    class RuntimeFontAssets {
        <<runtime clones>>
        PanelSettings
        PanelTextSettings
        Dynamic FontAsset
    }
    class InputActionAsset {
        <<Unity Input System>>
    }

    KeyConfigPanel *-- KeyConfigController
    KeyConfigPanel *-- KeyConfigView
    KeyConfigPanel *-- RuntimeFontAssets
    KeyConfigController *-- InputBindingService
    KeyConfigController *-- InputRebindController
    InputRebindController *-- RebindAliasSuppression
    InputBindingService --> InputActionAsset
    InputRebindController --> InputActionAsset
```

`InputRebindController`は全リバインドでDualShock／DualSense固有の`leftTriggerButton`／`rightTriggerButton`を候補から除外します。`RebindAliasSuppression`は複合Bindingの各パート確定時に同じStateイベントで変化したControlを記録し、後続パートへの重複登録を防ぎます。

## 公開インターフェース構成

```mermaid
classDiagram
    class IUiMenu {
        <<interface / com.koiusa.ui.core>>
        +IsVisible bool
        +Open()
        +Close()
        +FocusInitial()
        +Closed event
    }
    class KeyConfigPanel {
        <<public MonoBehaviour>>
        +SetPersistence(load, save)
        +Toggle()
    }
    class KeyConfigSettings {
        <<public ScriptableObject>>
        +InputActionAsset
        +NonRebindableActionMaps
    }
    class KeyConfigController {
        <<public IDisposable>>
        +GetBindingGroups()
        +GetBindings(bindingGroup)
        +StartRebind(bindingId, bindingGroup)
        +ResolveConflict(resolution)
        +Reset(bindingId)
        +ExportOverrides() string
        +ImportOverrides(json)
        +BindingChanged event
        +ConflictDetected event
        +RebindFinished event
    }
    class KeyConfigBindingId {
        <<public readonly struct>>
        +ActionId Guid
        +BindingId Guid
    }
    class KeyConfigBinding {
        <<public read-only DTO>>
        +Id KeyConfigBindingId
        +EffectivePaths
        +IsRebindable bool
    }
    class KeyConfigConflict {
        <<public DTO>>
        +Target KeyConfigBinding
        +Existing KeyConfigBinding
    }
    class KeyConfigRebindResult {
        <<public DTO>>
        +Status KeyConfigRebindStatus
        +BindingId KeyConfigBindingId
        +ControlPath string
        +ErrorMessage string
    }
    class IKeyConfigLocalizer {
        <<public interface>>
        +Get(key) string
        +LocaleChanged event
    }
    class KeyConfigLocalization {
        <<public static facade>>
        +SetLocalizer(localizer)
    }

    IUiMenu <|.. KeyConfigPanel
    KeyConfigPanel --> KeyConfigSettings
    KeyConfigPanel *-- KeyConfigController
    KeyConfigController --> KeyConfigBindingId
    KeyConfigController --> KeyConfigBinding
    KeyConfigController --> KeyConfigConflict
    KeyConfigController --> KeyConfigRebindResult
    KeyConfigBinding *-- KeyConfigBindingId
    KeyConfigConflict *-- KeyConfigBinding
    KeyConfigRebindResult *-- KeyConfigBindingId
    KeyConfigLocalization --> IKeyConfigLocalizer
```

Unity UIを利用する側は`KeyConfigPanel`を入口とし、保存先だけを`SetPersistence`のdelegateで注入します。UIを持たない利用側は`KeyConfigController`を直接生成し、GUIDベースの`KeyConfigBindingId`と読み取り専用DTOを通じて操作します。Localizationの差し替えは`IKeyConfigLocalizer`を`KeyConfigLocalization`へ登録します。
