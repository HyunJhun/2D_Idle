# gameScene 플레이 가이드

Unity 6000.3.10f1에서 `Assets/Workflow/Scenes/gameScene.unity`를 열고 Play를 누릅니다. Game 뷰는 세로 720×1280 또는 9:16 비율을 권장합니다.

## 조작

- 기본 상태는 `AUTO ON`입니다. 검객과 몬스터가 서로 접근한 뒤 자동으로 공격합니다.
- 전투 화면을 클릭/터치하면 추가 기본 공격이 나갑니다. `AUTO OFF`에서도 사용할 수 있으며 연타 간격은 0.35초입니다.
- 하단 스킬 버튼을 누르면 실제 피해와 연출이 발생합니다. 버튼에 남은 쿨타임이 표시됩니다.
- 처치 골드를 사용해 성장 항목별 `+1 / +10 / +100`으로 공격력·체력·치명타를 강화할 수 있습니다. 각 버튼에는 해당 수량의 총비용이 표시됩니다.
- 성장 버튼은 누르는 순간 1회 실행하며, 2초 유지 후부터 0.25초 간격으로 반복됩니다. 손을 떼거나 버튼 밖으로 이동·드래그하거나 창 포커스를 잃으면 중단됩니다. 치명타는 Lv.200(100%)까지이며, 잔액 부족 또는 최대 레벨을 넘는 묶음 강화는 골드를 소모하지 않습니다.
- 성장 제목 오른쪽 `DEV / RESET STATS`는 공격 Lv.120·체력 Lv.80·치명타 Lv.25와 최대 생명력으로 복원합니다. 골드와 진행도는 유지됩니다. Unity Editor와 Development Build에서만 표시됩니다.
- 보스 실패 후 `RETRY BOSS`로 다시 도전합니다. Play를 종료하고 재시작하면 예시 진행도와 수치가 초기화됩니다.

| 스킬 | 효과 | 쿨타임 |
|---|---|---|
| SLASH | 공격력 2.4배 피해 | 4초 |
| STORM | 공격력 3.5배 피해 | 7초 |
| FLAME | 공격력 5배 피해 | 10초 |
| FROST | 공격력 1.8배 피해, 현재 적의 공격을 3초 정지 | 8초 |
| RUSH | 공격력 2.8배 피해, 플레이어 최대 체력 20% 회복 | 8초 |

## 웨이브와 스테이지

| 구간 | 일반 웨이브 | 보스 |
|---|---|---|
| 1-1 ~ 1-5 / 숲 | Slime, Skeleton, Orc | Elite Orc |
| 2-1 ~ 2-5 / 폐허 | Demon_A, Hellhound, Armored Skeleton | Minotaur |

플레이어는 `Swordsman`입니다. 캐릭터는 `Workflow/Sprites`의 원본 애니메이션 시트를 참조하며, 필요한 프레임은 캐릭터 설정 에셋에 서브 에셋으로 저장합니다. Workflow 스프라이트와 현재 씬의 PNG 스프라이트에 Point 필터를 임포터 설정으로 저장했으며, 밉맵을 끄고 기본 압축을 해제했습니다. 배경은 기존 Tiles.png의 잔디·흙 타일을 사용하며, 시트 경계가 비치지 않도록 두 타일만 별도 텍스처로 추출했습니다.

일반 웨이브는 실제 몬스터 3마리를 표시하고 앞쪽 적부터 전투합니다. 접근 중에는 피해를 주고받지 않으며, 적이 사망하면 다음 적이 접근합니다. 일반 구간은 3마리 처치 후 다음으로 넘어갑니다. 1-5와 2-5는 단일 보스와 45초 동안 전투합니다. 이동과 등장 연출도 보스 제한시간에 포함됩니다.

보스를 제한시간 안에 처치하지 못하면 해당 장의 4번 구간에서 반복 사냥합니다. 재도전은 버튼으로 시작합니다. 2-5 클리어 후에는 2-4로 돌아가 반복 사냥할 수 있습니다. 마지막 보스는 스킬과 강화를 활용하는 것을 전제로 한 예시 밸런스입니다.

## 편집할 곳

- `Workflow/Data/StageCatalog.asset`: 10개 구간의 HP, ATK, 처치 목표, 골드, 보스 시간.
- `Workflow/Data/Characters/*.asset`: 원본 스프라이트 프레임, 애니메이션 속도, 표시 크기.
- `Workflow/Prefabs/Battle/Player.prefab`, `Monster.prefab`: SpriteRenderer, 그림자, 개별 체력바.
- 하이라키 `GRP_BattleArena`: 플레이어, `GRP_MonsterWave`, 배경, 이펙트, `CAM_Battle`.
- `SYS_StageBattle`: 전투 및 UI 연결. `IMG_BattleViewport`는 전투 카메라를 표시하는 UGUI RawImage입니다.

전투 카메라의 전용 `BattleActors` 레이어로 전투 영역을 UI 안에 표시합니다. Canvas는 기존 UGUI를 유지합니다. 사용자가 배치한 원본 `MainGame` 씬과 기존 빌드 씬 순서는 수정하지 않았습니다.

## 검증

`GameScenePlayableTests.Verify`는 원본 스프라이트 참조, 접근 거리, 몬스터 교체, 사망, 수동 공격, 스킬 쿨타임, 빙결, 두 보스의 45초 실패, 반복 사냥과 재도전을 검사하고 실제 전투 계산만으로 10개 구간을 끝까지 클리어합니다.

`GameScenePlayableTests.VerifyPlayMode`는 실제 Unity Play 모드의 Update를 실행하며 스킬 버튼의 직렬화된 이벤트를 사용해 두 장을 클리어합니다. 검증만 8배속으로 진행하며, 일반 Play의 속도는 1배입니다. 최종 결과는 `Documentation/gameScene-playable-test-results.txt`, 실제 전투 캡처는 `gameScene-playable-chapter-1.png`, `chapter-2.png`, `boss-1.png`, `boss-2.png`입니다.

현재 구현은 세션 단위 예시 게임입니다. 상점·장비·저장 기능은 아직 연결하지 않았습니다.
