using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;
/*
    これは釣りギミックにおいて、オブジェクトの生成、
    テキスト更新の呼び出しなどの機能を持つマネージャークラスです
    1. fishing_lodからsendCustomNetworkEventで呼び出される
    2. ランダムなものを実際に生成する
    3. textを更新する
*/

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class fishingManager : UdonSharpBehaviour
{
    // 1. 定数
    private const int MAX_TEXT_COUNT = 5;// テキストの最大数
    private const int KUROTEN_POOL_SIZE = 5;
    private const int AKATEN_POOL_SIZE = 4;
    private const int OTHER_POOL_SIZE = 6;
    private const int KUROTEN_RATE = 10;
    private const int AKATEN_RATE = 15;

    // 2. Inspectorで設定するフィールド
    [Header("釣り上げるオブジェクトの設定")]
    [SerializeField] private VRCObjectPool kurotenPool;
    [SerializeField] private VRCObjectPool akatenPool;
    [SerializeField] private VRCObjectPool otherPool;

    [Header("釣り上げたもののテキスト表示に関する設定")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("魚のスクリプトの設定")]
    [SerializeField] private Fish[] fishScripts;

    [Header("魚オブジェクトの追従範囲の設定")]
    [SerializeField] private Vector3 fishingZoneSize; // 魚オブジェクトの追従が有効な範囲のサイズ（竿ではない）
    [SerializeField] private Vector3 fishingZoneCenter;

    // 3. ネットワーク同期用
    //[UdonSynced] private float[] other_lastSpawnTime = new float[OTHER_POOL_SIZE];// その他のオブジェクトの最後に生成した時間を保持する配列
    [UdonSynced] private string resultTextString;// 新しく釣り上げられたもののテキストを保持する変数
    [UdonSynced] private int[] fishesState;// index=fishID, 釣られている場合には釣ったプレイヤーのIDを保持、つられていない場合には-1

    // 4. 状態管理用のprivateフィールド
    private int kuroten_nextSpawnObjectIndex = 0;
    private int akaten_nextSpawnObjectIndex = 0;
    private int other_nextSpawnObjectIndex = 0;
    private string[] resultTextArray = new string[MAX_TEXT_COUNT] { "", "", "", "", "" };

    private Bounds fishingAreaBounds;

    void Start()
    {
        fishingAreaBounds = new Bounds(fishingZoneCenter, fishingZoneSize);

        int n = fishScripts.Length;
        fishesState = new int[n];
        for (int i = 0; i < n; i++)
        {
            fishScripts[i].fishID = i; // 各魚のスクリプトに一意なIDを設定
            fishesState[i] = -1; // 初期化: どのプレイヤーも追従していない状態
            //Debug.Log("Fish ID: " + i + " が設定されました。");
        }
    }

    void FixedUpdate()
    {
        CheckFishPosition();
    }

    // 変数が同期されたときに呼ばれるメソッド
    public override void OnDeserialization()
    {
        // 配列の中で一番古いものを削除して、後ろに詰める
        for (int i = MAX_TEXT_COUNT - 1; i > 0; i--)
        {
            if (string.IsNullOrEmpty(resultTextArray[i - 1])) continue; // 空の要素はスキップ
            resultTextArray[i] = resultTextArray[i - 1];
        }
        resultTextArray[0] = resultTextString; // 新しいテキストを先頭に追加

        resultText.text = string.Join("\n", resultTextArray); // 配列からテキストを結合して表示

        Debug.Log("OnDeserializationが呼ばれました");
        LogArrayContentsInt(fishesState, "followingPlayerForFish");
    }

    void CheckFishPosition()
    {
        if (!Networking.IsOwner(gameObject)) return;

        // 各魚の位置をチェックして、釣りゾーンの外に出ていたらプールに返却する
        for (int i = 0; i < fishScripts.Length; i++)
        {
            Fish fish = fishScripts[i];
            if (fish == null) continue;

            if (!fishingAreaBounds.Contains(fish.transform.position))
            {
                // 魚をプールに返却する
                ReturnFishToPoolByID(i);
                fishesState[i] = -1; // 追従していたプレイヤーをリセット

                Debug.Log("Fish ID: " + i + " が釣りゾーンの外に出たため、プールに返却されました。");
            }
        }
    }

    [NetworkCallable]
    public void FishCaught(Vector3 lodPosition, Vector3 fishingLineEntryPoint, string playerName)
    {
        if (!Networking.IsOwner(gameObject)) return;// オーナーではないときは終了

        VRCPlayerApi caller = NetworkCalling.CallingPlayer;

        // 釣り上げられたオブジェクトを生成する
        GameObject spawnedObject = SpawnObject();
        if (spawnedObject == null)
        {
            Debug.Log("ERROR 釣り上げられたオブジェクトを生成できませんでした。");
            return; // オブジェクトが生成できなかった場合は終了
        }

        // オーナーと位置設定の共通処理
        SetOwnerAndPosition(spawnedObject, fishingLineEntryPoint, caller);

        Fish fish = spawnedObject.GetComponent<Fish>();
        int fishid = fish.fishID;

        // すでに追従している魚がある、プレイヤーが魚を釣った場合には、もともとの追従を停止する
        for (int i = 0; i < fishesState.Length; i++)
        {
            if (fishesState[i] == caller.playerId)
            {
                fishesState[i] = -1; // 追従していたプレイヤーをリセット

                // poolによる非アクティブ化で自動的に追跡のストップ処理が呼び出されるため、
                // SendCustomNetworkEventでStopFollowingPlayerを呼び出す必要はない
                ReturnFishToPoolByID(i);
                break;
            }
        }

        // 釣った魚の釣り竿への移動を開始する
        // (Fishスクリプトの内部で自動的に追従が開始される）
        // 現在ではSendCustomNetworkEventが届く順番を考慮していないことに注意
        fishesState[fishid] = caller.playerId; // 追従するプレイヤーのIDを更新
        fish.SendCustomNetworkEvent(NetworkEventTarget.All, "StartMovingToPos", fishingLineEntryPoint, lodPosition, caller.playerId);

        string modifiredObjectName = spawnedObject.name.Substring(0, spawnedObject.name.Length - 1); // 最後のナンバリング文字を削除
        AddResultText(modifiredObjectName, playerName);// テキストを追加
        RequestSerialization();

        Debug.Log("FishCaughtが呼ばれました" + lodPosition + " " + playerName);
        Debug.Log("呼び出したプレイヤーのID: " + caller.playerId);
        Debug.Log("魚のID: " + fishid);
        Debug.Log("同期する前の配列の内容: ");
        LogArrayContentsInt(fishesState, "followingPlayerForFish");
    }

    // 実際に釣られたオブジェクトを生成するメソッド
    private GameObject SpawnObject()
    {
        int randomValue = Random.Range(0, 20);
        GameObject obj = null;

        if (randomValue < KUROTEN_RATE) // 50%の確率で黒点
        {
            obj = SpawnFromKurotenPool();
        }
        else if (randomValue < AKATEN_RATE) // 25%の確率で赤点
        {
            obj = SpawnFromAkatenPool();
        }
        else // 25%の確率でその他のオブジェクト
        {
            obj = SpawnFromOtherPool();
        }

        return obj; // 生成したオブジェクトを返す
    }

    // 黒点用のスポーン処理
    private GameObject SpawnFromKurotenPool()
    {
        // 釣る予定のオブジェクトが既にほかのプレイヤーにつられている場合には一度Poolに返却する
        if (fishesState[kuroten_nextSpawnObjectIndex] != -1)
        {
            kurotenPool.Return(kurotenPool.Pool[kuroten_nextSpawnObjectIndex]);
        }

        GameObject obj = kurotenPool.TryToSpawn();

        if (obj == null) return null;

        kuroten_nextSpawnObjectIndex++;
        if (kuroten_nextSpawnObjectIndex >= KUROTEN_POOL_SIZE) kuroten_nextSpawnObjectIndex = 0; // インデックスを循環させる

        return obj; // 生成したオブジェクトを返す
    }

    // 赤点用のスポーン処理
    private GameObject SpawnFromAkatenPool()
    {
        // 釣る予定のオブジェクトが既にほかのプレイヤーにつられている場合には一度Poolに返却する
        if (fishesState[KUROTEN_POOL_SIZE + akaten_nextSpawnObjectIndex] != -1)
        {
            akatenPool.Return(akatenPool.Pool[akaten_nextSpawnObjectIndex]);
        }

        GameObject obj = akatenPool.TryToSpawn();

        if (obj == null) return null;

        akaten_nextSpawnObjectIndex++;
        if (akaten_nextSpawnObjectIndex >= AKATEN_POOL_SIZE) akaten_nextSpawnObjectIndex = 0; // インデックスを循環させる

        return obj; // 生成したオブジェクトを返す
    }

    // otherPool専用のスポーン処理
    private GameObject SpawnFromOtherPool()
    {
        //int randomIndex = Random.Range(0, OTHER_POOL_SIZE); // 生成するオブジェクトの仮のインデックスをランダムに選択

        // 釣る予定のオブジェクトが既にほかのプレイヤーにつられている場合には一度Poolに返却する
        if (fishesState[KUROTEN_POOL_SIZE + AKATEN_POOL_SIZE + other_nextSpawnObjectIndex] != -1)
        {
            otherPool.Return(otherPool.Pool[other_nextSpawnObjectIndex]);
        }

        GameObject obj = otherPool.TryToSpawn();

        if (obj == null) return null;

        other_nextSpawnObjectIndex++;
        if (other_nextSpawnObjectIndex >= OTHER_POOL_SIZE) other_nextSpawnObjectIndex = 0; // インデックスを循環させる

        return obj;
    }

    // オーナーと位置設定の共通処理
    private void SetOwnerAndPosition(GameObject obj, Vector3 catchPosition, VRCPlayerApi caller)
    {
        if (!Networking.IsOwner(caller, obj))
        {
            Networking.SetOwner(caller, obj); // オーナーを設定
        }
        obj.transform.position = catchPosition + new Vector3(0, 0.3f, 0); // 位置を設定
    }

    // オーナーから実行される、テキストを設定する変数
    private void AddResultText(string caughtItemName, string playerName)
    {
        resultTextString = $"{playerName} が  {caughtItemName}  を釣り上げました！"; // 同期変数に設定

        // 配列の中で一番古いものを削除して、後ろに詰める
        for (int i = MAX_TEXT_COUNT - 1; i > 0; i--)
        {
            if (string.IsNullOrEmpty(resultTextArray[i - 1])) continue; // 空の要素はスキップ
            resultTextArray[i] = resultTextArray[i - 1];
        }
        resultTextArray[0] = resultTextString; // 新しいテキストを先頭に追加

        resultText.text = string.Join("\n", resultTextArray); // 配列からテキストを結合して表示
    }

    // IDに応じて魚をプールに返却する
    private void ReturnFishToPoolByID(int fishID)
    {
        VRCObjectPool pool;
        int poolIndex;

        // 黒点のプールの場合
        if (fishID < KUROTEN_POOL_SIZE)
        {
            pool = kurotenPool;
            poolIndex = fishID;
        }
        // 赤点のプールの場合
        else if (fishID < KUROTEN_POOL_SIZE + AKATEN_POOL_SIZE)
        {
            pool = akatenPool;
            poolIndex = fishID - KUROTEN_POOL_SIZE;
        }
        // その他のプールの場合
        else
        {
            pool = otherPool;
            poolIndex = fishID - (AKATEN_POOL_SIZE + KUROTEN_POOL_SIZE);
        }

        GameObject fishObjectToReturn = pool.Pool[poolIndex]; // プールからオブジェクトを取得
        pool.Return(fishObjectToReturn);

        Debug.Log("Fish ID: " + fishID + " をプールに返却しました。");
    }

    /// <summary>
    /// 配列の中身を "[element1, element2, ...]" 形式でDebug.Logに出力します。
    /// </summary>
    /// <param name="array">表示する配列</param>
    /// <param name="arrayName">ログに出力する配列の名前 (オプション)</param>
    // --- int型配列のログ出力 ---
    public void LogArrayContentsInt(int[] array, string arrayName = "IntArray")
    {
        if (array == null)
        {
            Debug.Log($"{arrayName} is null.");
            return;
        }

        if (array.Length == 0)
        {
            Debug.Log($"{arrayName}: [] (Empty)");
            return;
        }

        string result = "[";
        for (int i = 0; i < array.Length; i++)
        {
            if (i > 0)
            {
                result += ", ";
            }
            result += array[i].ToString();
        }
        result += "]";

        Debug.Log($"{arrayName}: {result}");
    }

}
