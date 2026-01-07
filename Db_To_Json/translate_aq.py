#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AQ.json 汉化工具
基于原始英文 AQ.json，从 MySQL 数据库查询中文数据进行翻译
生成 AQ-cn.json

用法：python translate_aq.py
"""

import json
import sys
import time
from pathlib import Path

try:
    import pymysql
except ImportError:
    print("❌ 请先安装 pymysql: pip install pymysql")
    sys.exit(1)

# ===================== 配置区 =====================

# MySQL 数据库配置
MYSQL_CONFIG = {
    'host': '192.168.1.2',
    'port': 3306,
    'user': 'root',
    'password': 'password',
    'database': 'acore_world',
    'charset': 'utf8mb4'
}

# 文件路径
INPUT_FILE = 'Output/AQ.json'       # 英文版输入
OUTPUT_FILE = 'Output/AQ-cn.json'   # 中文版输出

# ===================== 翻译逻辑 =====================

class AQTranslator:
    def __init__(self, mysql_config):
        self.config = mysql_config
        self.conn = None
        self.cursor = None
        
        # 翻译缓存 (ID -> 中文名)
        self.quest_cache = {}
        self.creature_cache = {}
        self.gameobject_cache = {}
        self.item_cache = {}
        
        # 统计
        self.stats = {
            'quests_translated': 0,
            'creatures_translated': 0,
            'gameobjects_translated': 0,
            'items_translated': 0,
            'quests_not_found': 0,
            'creatures_not_found': 0,
            'gameobjects_not_found': 0,
            'items_not_found': 0
        }

    def connect(self):
        """连接数据库"""
        print(f"📡 正在连接 MySQL: {self.config['host']}:{self.config['port']}...")
        try:
            self.conn = pymysql.connect(**self.config)
            self.cursor = self.conn.cursor(pymysql.cursors.DictCursor)
            print(f"   ✅ 连接成功！数据库: {self.config['database']}")
            return True
        except Exception as e:
            print(f"   ❌ 连接失败: {e}")
            return False

    def disconnect(self):
        """断开连接"""
        if self.cursor:
            self.cursor.close()
        if self.conn:
            self.conn.close()

    def load_translation_cache(self):
        """预加载所有翻译数据到内存"""
        print("\n📚 正在加载翻译数据...")
        start_time = time.time()

        # 1. 加载任务翻译
        print("   - 任务 (quest_template)...", end=' ')
        self.cursor.execute("""
            SELECT ID, LogTitle, ObjectiveText1, ObjectiveText2, 
                   ObjectiveText3, ObjectiveText4, AreaDescription
            FROM quest_template
        """)
        for row in self.cursor.fetchall():
            self.quest_cache[row['ID']] = row
        print(f"✅ {len(self.quest_cache)} 条")

        # 2. 加载生物翻译
        print("   - 生物 (creature_template)...", end=' ')
        self.cursor.execute("SELECT entry, name FROM creature_template")
        for row in self.cursor.fetchall():
            self.creature_cache[row['entry']] = row['name']
        print(f"✅ {len(self.creature_cache)} 条")

        # 3. 加载游戏对象翻译
        print("   - 游戏对象 (gameobject_template)...", end=' ')
        self.cursor.execute("SELECT entry, name FROM gameobject_template")
        for row in self.cursor.fetchall():
            self.gameobject_cache[row['entry']] = row['name']
        print(f"✅ {len(self.gameobject_cache)} 条")

        # 4. 加载物品翻译
        print("   - 物品 (item_template)...", end=' ')
        self.cursor.execute("SELECT entry, name FROM item_template")
        for row in self.cursor.fetchall():
            self.item_cache[row['entry']] = row['name']
        print(f"✅ {len(self.item_cache)} 条")

        elapsed = time.time() - start_time
        print(f"\n   📊 加载完成，耗时 {elapsed:.2f} 秒")

    def translate_quest(self, quest):
        """翻译单个任务"""
        quest_id = quest.get('ID') or quest.get('Id') or quest.get('id')
        if quest_id and quest_id in self.quest_cache:
            cn = self.quest_cache[quest_id]
            
            # 翻译标题
            if cn.get('LogTitle'):
                quest['LogTitle'] = cn['LogTitle']
            
            # 翻译目标文本
            for i in range(1, 5):
                key = f'ObjectiveText{i}'
                if cn.get(key):
                    quest[key] = cn[key]
            
            # 翻译区域描述
            if cn.get('AreaDescription'):
                quest['AreaDescription'] = cn['AreaDescription']
            
            self.stats['quests_translated'] += 1
        else:
            self.stats['quests_not_found'] += 1
        
        return quest

    def translate_creature(self, creature):
        """翻译单个生物"""
        entry = creature.get('entry') or creature.get('Entry') or creature.get('id')
        if entry and entry in self.creature_cache:
            creature['name'] = self.creature_cache[entry]
            self.stats['creatures_translated'] += 1
        else:
            self.stats['creatures_not_found'] += 1
        return creature

    def translate_gameobject(self, gameobject):
        """翻译单个游戏对象"""
        entry = gameobject.get('entry') or gameobject.get('Entry') or gameobject.get('id')
        if entry and entry in self.gameobject_cache:
            gameobject['name'] = self.gameobject_cache[entry]
            self.stats['gameobjects_translated'] += 1
        else:
            self.stats['gameobjects_not_found'] += 1
        return gameobject

    def translate_item(self, item):
        """翻译单个物品"""
        entry = item.get('entry') or item.get('Entry') or item.get('id')
        if entry and entry in self.item_cache:
            # 物品名可能是 Name 或 name
            if 'Name' in item:
                item['Name'] = self.item_cache[entry]
            elif 'name' in item:
                item['name'] = self.item_cache[entry]
            self.stats['items_translated'] += 1
        else:
            self.stats['items_not_found'] += 1
        return item

    def translate_json(self, data):
        """翻译整个 JSON 数据"""
        print("\n🔄 正在翻译...")
        
        # 1. 翻译任务
        if 'QuestTemplates' in data:
            print(f"   - 任务: {len(data['QuestTemplates'])} 条...", end=' ')
            for quest in data['QuestTemplates']:
                self.translate_quest(quest)
            print("✅")
        
        # 2. 翻译生物
        if 'CreatureTemplates' in data:
            print(f"   - 生物: {len(data['CreatureTemplates'])} 条...", end=' ')
            for creature in data['CreatureTemplates']:
                self.translate_creature(creature)
            print("✅")
        
        # 3. 翻译游戏对象
        if 'GameObjectTemplates' in data:
            print(f"   - 游戏对象: {len(data['GameObjectTemplates'])} 条...", end=' ')
            for go in data['GameObjectTemplates']:
                self.translate_gameobject(go)
            print("✅")
        
        # 4. 翻译物品
        if 'ItemTemplates' in data:
            print(f"   - 物品: {len(data['ItemTemplates'])} 条...", end=' ')
            for item in data['ItemTemplates']:
                self.translate_item(item)
            print("✅")
        
        return data


def main():
    print("="*60)
    print("  AQ.json 汉化工具 v1.0")
    print("  基于原始英文版翻译，保留完整结构")
    print("="*60)
    
    # 1. 检查输入文件
    print(f"\n📂 输入文件: {INPUT_FILE}")
    if not Path(INPUT_FILE).exists():
        print(f"   ❌ 文件不存在！")
        return
    
    # 2. 加载 JSON
    print("   📖 正在加载...", end=' ')
    start_time = time.time()
    try:
        with open(INPUT_FILE, 'r', encoding='utf-8') as f:
            data = json.load(f)
        print(f"✅ ({time.time()-start_time:.2f}秒)")
    except Exception as e:
        print(f"❌ 加载失败: {e}")
        return
    
    # 3. 显示结构
    print("\n📊 JSON 结构:")
    for key, value in data.items():
        if isinstance(value, list):
            print(f"   - {key}: {len(value)} 条")
        else:
            print(f"   - {key}: {type(value).__name__}")
    
    # 4. 创建翻译器并连接数据库
    translator = AQTranslator(MYSQL_CONFIG)
    if not translator.connect():
        return
    
    try:
        # 5. 加载翻译缓存
        translator.load_translation_cache()
        
        # 6. 翻译
        translated_data = translator.translate_json(data)
        
        # 7. 保存结果
        print(f"\n💾 正在保存: {OUTPUT_FILE}...", end=' ')
        start_time = time.time()
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            json.dump(translated_data, f, ensure_ascii=False, indent=2)
        print(f"✅ ({time.time()-start_time:.2f}秒)")
        
        # 8. 显示统计
        print("\n" + "="*60)
        print("📊 翻译统计:")
        print(f"   任务:     {translator.stats['quests_translated']:>5} 翻译, {translator.stats['quests_not_found']:>5} 未找到")
        print(f"   生物:     {translator.stats['creatures_translated']:>5} 翻译, {translator.stats['creatures_not_found']:>5} 未找到")
        print(f"   游戏对象: {translator.stats['gameobjects_translated']:>5} 翻译, {translator.stats['gameobjects_not_found']:>5} 未找到")
        print(f"   物品:     {translator.stats['items_translated']:>5} 翻译, {translator.stats['items_not_found']:>5} 未找到")
        print("="*60)
        print(f"\n✅ 汉化完成！输出文件: {OUTPUT_FILE}")
        
    finally:
        translator.disconnect()


if __name__ == "__main__":
    main()
