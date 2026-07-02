import sqlite3

DB_NAME = "database/atvsld.db"


def get_connection():
    conn = sqlite3.connect(DB_NAME)
    conn.row_factory = sqlite3.Row
    return conn


def create_tables():
    conn = get_connection()
    cur = conn.cursor()

    cur.execute("""
    CREATE TABLE IF NOT EXISTS cong_doan(
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        ma TEXT UNIQUE,
        ten TEXT,
        mo_ta TEXT
    )
    """)

    conn.commit()
    conn.close()


# ===========================
# CÔNG ĐOẠN
# ===========================

def get_all_cong_doan():

    conn = get_connection()
    cur = conn.cursor()

    cur.execute("""
        SELECT *
        FROM cong_doan
        ORDER BY ma
    """)

    rows = cur.fetchall()

    conn.close()

    return rows


def insert_cong_doan(ma, ten, mota):

    conn = get_connection()
    cur = conn.cursor()

    cur.execute("""
        INSERT INTO cong_doan(
            ma,
            ten,
            mo_ta
        )
        VALUES(?,?,?)
    """, (ma, ten, mota))

    conn.commit()
    conn.close()


def update_cong_doan(id_, ma, ten, mota):

    conn = get_connection()
    cur = conn.cursor()

    cur.execute("""
        UPDATE cong_doan
        SET
            ma=?,
            ten=?,
            mo_ta=?
        WHERE id=?
    """, (ma, ten, mota, id_))

    conn.commit()
    conn.close()


def delete_cong_doan(id_):

    conn = get_connection()
    cur = conn.cursor()

    cur.execute("""
        DELETE FROM cong_doan
        WHERE id=?
    """, (id_,))

    conn.commit()
    conn.close()