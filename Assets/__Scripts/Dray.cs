using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dray : MonoBehaviour, IFacingMover, IKeyMaster
{
    public enum eMode { idle, move, attack, transition, knockback }

    [Header("Set in Inspector")]
    public float speed = 5;
    public float attackDuration = 0.25f;//время анимации атаки
    public float attackDelay = 0.5f;    //гкд
    public float knockbackSpeed = 10;
    public float knockbackDuration = 0.25f;
    public float invincibleDuration = 0.5f;
    public float transitionDelay = 0.5f;
    public int maxHealth = 10;

    [Header("Set Dynamically")]
    public int dirHeld = -1; // направление движения
    public int facing = 1;  //направление движения = атаки
    public eMode mode = eMode.idle;
    public int numKeys = 0;
    public bool invincible = false;
    public bool hasGrappler = false;
    public Vector3 lastSafeLoc;
    public int lastSafeFacing;

    [SerializeField]
    private int _health;

    private float timeAtkDone = 0;  //время завершения анимации атаки
    private float timeAtkNext = 0;  //время начала следующей атаки
    private float knockbackDone = 0;
    private float invincibleDone = 0;
    private Vector3 knockbackVel;

    private float transitionDone = 0;
    private Vector2 transitionPos;

    private Rigidbody rigid;
    private Animator anim;
    private InRoom inRm;
    private SpriteRenderer sRend;

    private Vector3[] directions = new Vector3[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down };
    private KeyCode[] keys = new KeyCode[] { KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.DownArrow };
    public int health
    {
        get { return _health; }
        set { _health = value; }
    }
    private void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        inRm = GetComponent<InRoom>();
        sRend = GetComponent<SpriteRenderer>();
        lastSafeLoc = transform.position;
        lastSafeFacing = facing;
        health = maxHealth;
    }
    // реализация интерфейса IFacingMover
    public int GetFacing()
    {
        return facing;
    }
    public bool moving
    {
        get { return (mode == eMode.move); }
    }
    public float GetSpeed() { return speed; }
    public float gridMult { get { return inRm.gridMult; } }
    public Vector2 roomPos { get { return inRm.roomPos; }
        set { inRm.roomPos = value; }
    }
    public Vector2 roomNum
    {
        get { return inRm.roomNum; }
        set { inRm.roomNum = value; }
    }
    public Vector2 GetRoomPosOnGrid(float mult = -1) { return inRm.GetRoomPosOnGrid(mult); }
    
    void Update()
    {
        // АТК: неуязвимость и отталкивание
        if (invincible && Time.time > invincibleDone) invincible = false;
        sRend.color = invincible ? Color.red : Color.white;
        if (mode == eMode.knockback)
        {
            rigid.velocity = knockbackVel;
            if (Time.time < knockbackDone) return;
        }

        if (mode == eMode.transition)
        {
            //удерживать на месте при переходе между комнатами до transitionDone с
            rigid.velocity = Vector3.zero;
            anim.speed = 0;
            roomPos = transitionPos;
            if (Time.time < transitionDone) return;
            mode = eMode.idle;
        }
        //------Обработка ввода с клавиатуры и управление режимами eMode----
        dirHeld = -1;
        for (int i = 0; i < 4; i++)
            if (Input.GetKey(keys[i])) dirHeld = i;

        //Обработка атак
        if(Input.GetKeyDown(KeyCode.Z) && Time.time >= timeAtkNext)
        {
            mode = eMode.attack;
            timeAtkDone = Time.time + attackDuration;
            timeAtkNext = Time.time + attackDelay;
        }

        //Завершение атаки
        if (Time.time >= timeAtkDone)
            mode = eMode.idle;

        //Выбор режима,если Дрей не атакует
        if(mode != eMode.attack)
        {
            if (dirHeld == -1)
                mode = eMode.idle;
            else
            {
                facing = dirHeld;
                mode = eMode.move;
            }    
        }

        //------Действия в текущем режиме------
        Vector3 vel = Vector3.zero;
        switch(mode)
        {
            case eMode.attack:
                //ручная смена анимации
                anim.CrossFade("Dray_Attack_" + facing, 0);
                anim.speed = 0;
                break;
            case eMode.idle:
                anim.CrossFade("Dray_Walk_" + facing, 0);
                anim.speed = 0;
                break;
            case eMode.move:
                vel = directions[dirHeld];
                anim.CrossFade("Dray_Walk_" + facing, 0);
                anim.speed = 1;
                break;
        }
        if (dirHeld > -1) vel = directions[dirHeld];
        rigid.velocity = vel * speed;

    }
    private void LateUpdate()
    {
        Vector2 rPos = GetRoomPosOnGrid(0.5f);
        int doorNum;
        for (doorNum = 0; doorNum < 4; doorNum++)
            if (rPos == InRoom.DOORS[doorNum]) 
                break;
        if (doorNum > 3 || doorNum != facing) // Дрей не находится в двери/не повернут к выходу
            return;
        Vector2 rm = roomNum;
        switch(doorNum)
        {
            case 0:
                rm.x += 1;
                break;
            case 1:
                rm.y += 1;
                break;
            case 2:
                rm.x -= 1;
                break;
            case 3:
                rm.y -= 1;
                break;
        }
        if((rm.x >= 0 && rm.x <= InRoom.MAX_RM_X)&&(rm.y >= 0 && rm.y <= InRoom.MAX_RM_Y))
        {
            roomNum = rm;
            transitionPos = InRoom.DOORS[(doorNum + 2) % 4];
            roomPos = transitionPos;
            lastSafeLoc = transform.position;
            lastSafeFacing = facing;
            mode = eMode.transition;
            transitionDone = Time.time + transitionDelay;
        }
    }

    public int keyCount
    {
        get { return numKeys; }
        set { numKeys = value; }
    }
    private void OnCollisionEnter(Collision collision)
    {
        //-----------------ДЕФ: получение урона врагом----------------
        if (invincible) return;
        DamageEffect dEf = collision.gameObject.GetComponent<DamageEffect>();
        if (dEf == null) return;
        health -= dEf.damage;
        invincible = true;
        invincibleDone = Time.time + invincibleDuration;

        //отбрасывание при получении урона
        if (dEf.knockback)
        {
            Vector3 delta = transform.position - collision.transform.position;
            if(Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                delta.x = (delta.x > 0) ? 1 : -1;
                delta.y = 0;
            }
            else
            {
                delta.x = 0;
                delta.y = (delta.y > 0) ? 1 : -1;
            }
            knockbackVel = delta * knockbackSpeed;
            rigid.velocity = knockbackVel;
            mode = eMode.knockback;
            knockbackDone = Time.time + knockbackDuration;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        PickUp pup = other.GetComponent<PickUp>();
        if (pup == null) return;
        switch (pup.itemType)
        {
            case PickUp.eType.health:
                health = Mathf.Min(health + 2, maxHealth);
                break;
            case PickUp.eType.key:
                keyCount++;
                break;
            case PickUp.eType.grappler:
                hasGrappler = true;
                break;
        }
        Destroy(other.gameObject);
    }
    public void ResetInRoom(int healthLoss = 0)
    {
        transform.position = lastSafeLoc;
        facing = lastSafeFacing;
        health -= healthLoss;
        invincible = true;
        invincibleDone = Time.time + invincibleDuration;
    }
}