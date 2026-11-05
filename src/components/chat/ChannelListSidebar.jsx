import React from 'react';
import { Search, Plus, EyeOff, Users, Pin, PinOff } from 'lucide-react';

export default function ChannelListSidebar({
  groups,
  activeGroupId,
  searchKeyword,
  setSearchKeyword,
  groupViewTab,
  setGroupViewTab,
  unreadCounts,
  onSelectGroup,
  onOpenCreateGroup,
  onTogglePinGroup,
  onRestoreHiddenGroup
}) {
  const myHiddenGroupsCount = groups.filter(g => g.isHidden && g.ownerId === 'me').length;

  const filteredGroups = groups
    .filter(g => {
      const matchKeyword = (g.name || '').toLowerCase().includes(searchKeyword.toLowerCase());
      if (groupViewTab === 'hidden') {
        return matchKeyword && g.isHidden && g.ownerId === 'me';
      }
      return matchKeyword && !g.isHidden;
    })
    .sort((a, b) => {
      if (a.isPinned && !b.isPinned) return -1;
      if (!a.isPinned && b.isPinned) return 1;
      if (a.isPinned && b.isPinned) {
        return (b.pinnedAt || 0) - (a.pinnedAt || 0);
      }
      return 0;
    });

  // Cắt ngắn nội dung tin nhắn cuối cùng đồng bộ (tối đa 28 ký tự)
  const formatLastMessage = (msg) => {
    if (!msg || msg.trim() === '') return 'Chưa có tin nhắn mới';
    const clean = msg.replace(/\n/g, ' ').trim();
    if (clean.length > 28) {
      return clean.substring(0, 28) + '...';
    }
    return clean;
  };

  return (
    <section className="w-72 md:w-80 bg-white border-r border-slate-200 flex flex-col shrink-0 z-10 select-none">
      {/* Search Header */}
      <div className="p-3 border-b border-slate-100 flex items-center gap-2">
        <div className="flex-1 relative">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
          <input 
            type="text"
            placeholder="Tìm kiếm kênh bán hàng..."
            value={searchKeyword}
            onChange={(e) => setSearchKeyword(e.target.value)}
            className="w-full bg-slate-100 text-xs pl-9 pr-3 py-1.5 rounded-lg focus:outline-none focus:ring-1 focus:ring-[#1D6374] text-slate-700"
          />
        </div>
        <button 
          onClick={onOpenCreateGroup}
          className="p-1.5 text-slate-600 hover:bg-slate-100 rounded-lg hover:text-[#1D6374] transition cursor-pointer"
          title="Tạo kênh bán hàng mới"
        >
          <Plus className="w-5 h-5" />
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-100 px-3 text-xs font-medium text-slate-500">
        <button 
          onClick={() => setGroupViewTab('active')}
          className={`py-2.5 px-3 border-b-2 transition ${
            groupViewTab === 'active' 
              ? 'border-[#1D6374] text-[#1D6374] font-bold' 
              : 'border-transparent hover:text-slate-700'
          }`}
        >
          Kênh của bạn ({filteredGroups.length})
        </button>
        
        {myHiddenGroupsCount > 0 && (
          <button 
            onClick={() => setGroupViewTab('hidden')}
            className={`py-2.5 px-3 border-b-2 transition flex items-center gap-1.5 ${
              groupViewTab === 'hidden' 
                ? 'border-[#1D6374] text-[#1D6374] font-bold' 
                : 'border-transparent hover:text-slate-700'
            }`}
          >
            <EyeOff className="w-3.5 h-3.5 text-slate-500" />
            <span>Kênh ẩn</span>
            <span className="bg-[#1D6374]/15 text-[#164E5B] text-[10px] font-bold px-1.5 py-0.2 rounded-full">
              {myHiddenGroupsCount}
            </span>
          </button>
        )}
      </div>

      {/* Channel List */}
      <div className="flex-1 overflow-y-auto divide-y divide-slate-50">
        {filteredGroups.length === 0 ? (
          <div className="p-8 text-center text-xs text-slate-400 space-y-1">
            <Users className="w-8 h-8 mx-auto text-slate-300 mb-2" />
            <p className="font-semibold text-slate-600">Bạn chưa có kênh nào</p>
            <p className="text-[11px] text-slate-400">Bạn cần được Trưởng nhóm thêm vào kênh để tham gia trao đổi.</p>
          </div>
        ) : (
          filteredGroups.map(grp => {
            const isSelected = String(grp.id).toLowerCase() === String(activeGroupId).toLowerCase();
            const unread = unreadCounts[String(grp.id).toLowerCase()] || 0;
            const hasUnread = unread > 0 && !isSelected;

            return (
              <div
                key={grp.id}
                onClick={() => onSelectGroup(grp.id)}
                className={`flex items-center gap-3 p-3 cursor-pointer transition select-none group relative ${
                  isSelected ? 'bg-[#ebf4f6]' : hasUnread ? 'bg-rose-50/30 hover:bg-rose-50/50' : 'hover:bg-slate-50'
                }`}
              >
                <div className="relative shrink-0">
                  <img 
                    src={grp.avatar} 
                    alt={grp.name}
                    className="w-11 h-11 rounded-full object-cover shadow-xs border border-slate-200"
                  />
                  <span className="absolute -bottom-1 -right-1 bg-white rounded-full p-0.5 border border-slate-200 text-[#1D6374]">
                    <Users className="w-2.5 h-2.5" />
                  </span>
                </div>

                <div className="flex-1 min-w-0 pr-1">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-1 min-w-0">
                      <h4 className={`text-xs truncate ${
                        isSelected 
                          ? 'font-bold text-[#164E5B]' 
                          : hasUnread 
                          ? 'font-bold text-slate-900' 
                          : 'font-medium text-slate-800'
                      }`}>
                        {grp.name}
                      </h4>
                      {grp.isPinned && !grp.isHidden && (
                        <span title="Kênh đã ghim" className="shrink-0 text-[#98C93C]">
                          <Pin className="w-3 h-3 fill-[#98C93C] rotate-45 text-[#86B828]" />
                        </span>
                      )}
                    </div>
                    <span className={`text-[10px] shrink-0 ml-1 ${hasUnread ? 'font-bold text-rose-500' : 'text-slate-400'}`}>
                      {grp.lastTime || ''}
                    </span>
                  </div>

                  {/* Cấu trúc mô tả đồng bộ: Luôn hiển thị nội dung tin nhắn cuối cùng được lưu trong DB */}
                  <div className="flex items-center justify-between mt-1 gap-1">
                    <p className={`text-[11px] truncate flex-1 ${
                      hasUnread ? 'font-semibold text-slate-800' : 'text-slate-400'
                    }`}>
                      {formatLastMessage(grp.lastMessage)}
                    </p>

                    {/* Huy hiệu số lượng tin nhắn chưa đọc màu đỏ chuẩn phong cách Zalo */}
                    {hasUnread && (
                      <span className="bg-[#ef4444] text-white text-[10px] font-bold px-1.5 min-w-[19px] h-[19px] rounded-full flex items-center justify-center shrink-0 shadow-sm animate-pulse">
                        {unread > 99 ? '99+' : unread}
                      </span>
                    )}
                  </div>
                </div>

                {!grp.isHidden && (
                  <div className="absolute right-2 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center bg-white/95 shadow-sm rounded-lg border border-slate-200 p-0.5 z-10">
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        onTogglePinGroup(grp.id);
                      }}
                      className="p-1.5 text-slate-500 hover:text-[#1D6374] hover:bg-slate-100 rounded transition cursor-pointer"
                      title={grp.isPinned ? "Bỏ ghim" : "Ghim lên đầu"}
                    >
                      {grp.isPinned ? <PinOff className="w-3.5 h-3.5 text-[#1D6374]" /> : <Pin className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                )}
              </div>
            );
          })
        )}
      </div>
    </section>
  );
}