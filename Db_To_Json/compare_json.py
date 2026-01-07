#!/usr/bin/env python3
"""
Compare AQ.json and AQ-cn.json statistics
比较 AQ.json 和 AQ-cn.json 的统计信息
"""

import json
import sys
from pathlib import Path

def load_json(filepath):
    """加载 JSON 文件"""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"❌ 文件不存在: {filepath}")
        return None
    except json.JSONDecodeError as e:
        print(f"❌ JSON 解析错误: {filepath}")
        print(f"   {e}")
        return None

def count_items(data):
    """统计 JSON 中各个类型的数量"""
    stats = {}
    
    if not isinstance(data, dict):
        return stats
    
    for key, value in data.items():
        if isinstance(value, list):
            stats[key] = len(value)
        elif isinstance(value, dict):
            stats[key] = len(value)
        else:
            stats[key] = 1
    
    return stats

def compare_json_files(file1, file2):
    """比较两个 JSON 文件"""
    print("="*70)
    print("  AQ.json vs AQ-cn.json 比较报告")
    print("="*70)
    print()
    
    # 加载文件
    print(f"📂 正在加载文件...")
    data1 = load_json(file1)
    data2 = load_json(file2)
    
    if data1 is None or data2 is None:
        return
    
    # 统计数量
    stats1 = count_items(data1)
    stats2 = count_items(data2)
    
    # 获取所有键
    all_keys = sorted(set(list(stats1.keys()) + list(stats2.keys())))
    
    # 打印表格
    print(f"{'类型':<30} {'AQ.json (英文)':<20} {'AQ-cn.json (中文)':<20} {'差异':>10}")
    print("-"*85)
    
    total_diff = 0
    
    for key in all_keys:
        count1 = stats1.get(key, 0)
        count2 = stats2.get(key, 0)
        diff = count2 - count1
        diff_symbol = "✅" if diff == 0 else ("⚠️" if abs(diff) < 100 else "❌")
        
        print(f"{key:<30} {count1:<20} {count2:<20} {diff:>9} {diff_symbol}")
        total_diff += abs(diff)
    
    print("-"*85)
    print()
    
    # 总结
    print("📊 总结:")
    print(f"   - 总类型数: {len(all_keys)}")
    print(f"   - 总差异数: {total_diff}")
    
    if total_diff == 0:
        print("   ✅ 两个文件数据量完全一致！")
    elif total_diff < 100:
        print("   ⚠️ 存在少量差异，可能是数据库版本差异")
    else:
        print("   ❌ 存在较大差异，需要检查数据库")
    
    print()
    print("="*70)

if __name__ == "__main__":
    # 文件路径
    if len(sys.argv) >= 3:
        file1 = sys.argv[1]
        file2 = sys.argv[2]
    else:
        # 默认路径
        file1 = "Output/AQ.json"
        file2 = "Output/AQ-cn.json"
    
    compare_json_files(file1, file2)
