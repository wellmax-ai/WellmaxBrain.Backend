import React, { useState, useEffect, useRef, useMemo } from 'react';
import { 
  Pin, Crown, ShieldCheck, Calendar, Phone, Video, 
  PanelRightClose, PanelRightOpen
} from 'lucide-react';

import { 
  INITIAL_SYSTEM_USERS, INITIAL_GROUPS, INITIAL_MESSAGES, INITIAL_CHANNEL_ORDERS,
  DEPARTMENT_OPTIONS, ROLE_GROUP_OPTIONS, REACTION_EMOJIS 
} from './constants/initialData';
import { playNotificationSound } from './utils/soundHelper';
import { signalRService } from './services/signalrService';
import { API_BASE } from './utils/apiConfig';

import LeftNavigation from './components/layout/LeftNavigation';
import ChannelListSidebar from './components/chat/ChannelListSidebar';
import MessageStream from './components/chat/MessageStream';
import ChatInputBar from './components/chat/ChatInputBar';
import RightOperationSidebar from './components/orders/RightOperationSidebar';

import AuthLoginView from './components/modals/AuthLoginView';
import UserManagementModal from './components/modals/UserManagementModal';
import AISettingsModal from './components/modals/AISettingsModal';
import ReminderModal from './components/modals/ReminderModal';
import CreateChannelModal from './components/modals/CreateChannelModal';
import AddMemberModal from './components/modals/AddMemberModal';
import ProfileModal from './components/modals/ProfileModal';
import UserFormModal from './components/modals/UserFormModal';
import ChangePasswordModal from './components/modals/ChangePasswordModal';

export default function App() {
  const [users, setUsers] = useState(() => {
    const saved = localStorage.getItem('wellmax_users');
    if (saved) {
      try { return JSON.parse(saved); } catch (e) { /* ignore */ }
    }
    return INITIAL_SYSTEM_USERS;
  });

  const [currentUserId, setCurrentUserId] = useState(() => {
    return localStorage.getItem('wellmax_current_uid') || 'me';
  });

  const [currentUserPhone, setCurrentUserPhone] = useState(() => {
    return localStorage.getItem('wellmax_current_phone') || '0988888888';
  });

  const [passwordFingerprint, setPasswordFingerprint] = useState(() => {
    return localStorage.getItem('wellmax_pass_fingerprint') || '';
  });

  const [isAuthenticated, setIsAuthenticated] = useState(() => {
    return localStorage.getItem('wellmax_auth') === 'true';
  });

  // State channels & messages
  const [groups, setGroups] = useState(INITIAL_GROUPS);
  const [activeGroupId, setActiveGroupId] = useState('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
  const [messages, setMessages] = useState({});
  const [channelOrders, setChannelOrders] = useState(INITIAL_CHANNEL_ORDERS);

  // Số lượng tin nhắn chưa đọc của riêng user hiện tại trên từng kênh
  const [unreadCounts, setUnreadCounts] = useState(() => {
    const saved = localStorage.getItem(`wellmax_unread_${currentUserPhone}`);
    if (saved) {
      try { return JSON.parse(saved); } catch (e) {}
    }
    return {};
  });

  // Filters & Tabs
  const [searchKeyword, setSearchKeyword] = useState('');
  const [groupViewTab, setGroupViewTab] = useState('active');
  const [rightPanelTab, setRightPanelTab] = useState('orders');
  const [isRightPanelOpen, setIsRightPanelOpen] = useState(true);
  const [isMembersSectionOpen, setIsMembersSectionOpen] = useState(false);

  // Inputs & Typing
  const [inputText, setInputText] = useState('');
  const [selectedImagePreview, setSelectedImagePreview] = useState(null);
  const [selectedAttachedFile, setSelectedAttachedFile] = useState(null);
  const [isBotTyping, setIsBotTyping] = useState(false);
  const [isSoundEnabled, setIsSoundEnabled] = useState(true);

  // Modals Visibility
  const [isCreateChannelOpen, setIsCreateChannelOpen] = useState(false);
  const [isAddMemberOpen, setIsAddMemberOpen] = useState(false);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);
  const [isUserMgmtOpen, setIsUserMgmtOpen] = useState(false);
  const [isUserFormOpen, setIsUserFormOpen] = useState(false);
  const [editingUser, setEditingUser] = useState(null);
  const [isChangePasswordOpen, setIsChangePasswordOpen] = useState(false);
  const [targetPasswordUser, setTargetPasswordUser] = useState(null);
  const [isAISettingsOpen, setIsAISettingsOpen] = useState(false);
  const [isReminderModalOpen, setIsReminderModalOpen] = useState(false);
  const [deleteMsgConfirmData, setDeleteMsgConfirmData] = useState(null);
  const [lightboxImageUrl, setLightboxImageUrl] = useState(null);

  // LLM Config
  const [llmConfig, setLlmConfig] = useState({
    provider: 'gemini',
    modelName: 'gemini-2.5-flash',
    apiKey: '',
    contextWindowSize: 8,
    temperature: 0.7,
    systemPrompt: `Bạn là Wellmax Brain AI System - Trợ lý trí tuệ nhân tạo chuyên sâu về Sức khỏe & Sắc đẹp tích hợp trong nhóm chat nội bộ.`
  });

  const messagesEndRef = useRef(null);
  const messageRefs = useRef({});
  const textareaRef = useRef(null);

  const currentUser = useMemo(() => {
    return users.find(u => 
      String(u.id).toLowerCase() === String(currentUserId).toLowerCase() ||
      (currentUserPhone && u.phone === currentUserPhone)
    ) || users[0];
  }, [users, currentUserId, currentUserPhone]);

  const activeGroup = groups.find(g => String(g.id).toLowerCase() === String(activeGroupId).toLowerCase()) || groups[0] || {
    id: activeGroupId,
    name: 'Kênh bán hàng',
    description: '',
    avatar: 'https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=120&q=80',
    members: []
  };

  const activeMessages = messages[String(activeGroupId).toLowerCase()] || [];
  const isSystemAdmin = !!currentUser?.isAdmin;

  // Lưu unreadCounts theo từng User Phone vào localStorage
  useEffect(() => {
    if (currentUserPhone) {
      localStorage.setItem(`wellmax_unread_${currentUserPhone}`, JSON.stringify(unreadCounts));
    }
  }, [unreadCounts, currentUserPhone]);

  // Cuộn mượt mà xuống cuối khi có tin nhắn mới
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [activeMessages.length]);

  const triggerForceLogout = (alertMessage) => {
    localStorage.removeItem('wellmax_auth');
    localStorage.removeItem('wellmax_current_uid');
    localStorage.removeItem('wellmax_current_phone');
    localStorage.removeItem('wellmax_pass_fingerprint');
    sessionStorage.clear();

    setIsAuthenticated(false);
    setIsUserMgmtOpen(false);
    setIsUserFormOpen(false);
    setIsChangePasswordOpen(false);
    setIsAISettingsOpen(false);
    setIsReminderModalOpen(false);

    if (alertMessage) {
      setTimeout(() => alert(alertMessage), 120);
    }
  };

  // 1. TẢI DANH SÁCH USERS TỪ BACKEND
  const fetchUsersFromDb = () => {
    fetch(`${API_BASE}/api/auth/users`)
      .then(res => res.ok ? res.json() : null)
      .then(dbUsers => {
        if (dbUsers && dbUsers.length > 0) {
          setUsers(dbUsers);
          localStorage.setItem('wellmax_users', JSON.stringify(dbUsers));
        }
      })
      .catch(() => {});
  };

  // 2. TẢI DANH SÁCH CHANNELS TỪ DATABASE POSTGRESQL
  const fetchChannelsFromDb = () => {
    fetch(`${API_BASE}/api/channels`)
      .then(res => res.ok ? res.json() : null)
      .then(dbChannels => {
        if (dbChannels && dbChannels.length > 0) {
          const formatted = dbChannels.map(c => ({
            ...c,
            members: c.members && c.members.length > 0 ? c.members : [c.ownerId, ...(c.deputyIds || []), '99999999-9999-9999-9999-999999999999'],
            pinnedMessageIds: []
          }));
          setGroups(formatted);
          if (!formatted.some(c => String(c.id).toLowerCase() === String(activeGroupId).toLowerCase())) {
            setActiveGroupId(formatted[0].id);
          }
        }
      })
      .catch(() => {});
  };

  useEffect(() => {
    if (isAuthenticated && currentUser?.id) {
      fetchUsersFromDb();
      fetchChannelsFromDb(currentUser.id);
    }
  }, [isAuthenticated, currentUser?.id]);

  // 3. TẢI LỊCH SỬ TIN NHẮN CHO KÊNH
  const fetchMessagesForChannel = (chanId) => {
    if (!chanId) return;
    const cleanId = String(chanId).toLowerCase();

    fetch(`${API_BASE}/api/messages/channel/${cleanId}`)
      .then(res => res.ok ? res.json() : null)
      .then(dbMsgs => {
        if (dbMsgs) {
          const formatted = dbMsgs.map(m => ({
            id: m.id,
            channelId: String(m.channelId).toLowerCase(),
            senderId: m.senderId,
            senderName: m.senderName,
            senderAvatar: m.senderAvatar,
            isMe: String(m.senderId).toLowerCase() === String(currentUser?.id).toLowerCase(),
            isBot: m.isBot,
            isSystem: m.isSystem,
            text: m.text,
            image: m.imageUrl,
            file: m.fileUrl ? { name: m.fileName || 'Tài liệu', size: m.fileSize || '1 MB', url: m.fileUrl } : null,
            reminder: m.reminderJson ? (typeof m.reminderJson === 'string' ? JSON.parse(m.reminderJson) : m.reminderJson) : null,
            createdAt: m.createdAtUnix,
            timestamp: new Date(m.createdAtUnix).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            reactions: m.reactions || {},
            isDeleted: m.isDeleted
          }));

          setMessages(prev => ({
            ...prev,
            [cleanId]: formatted
          }));
        }
      })
      .catch(() => {});
  };

  useEffect(() => {
    if (isAuthenticated && activeGroupId) {
      fetchMessagesForChannel(activeGroupId);
    }
  }, [activeGroupId, isAuthenticated, currentUser?.id]);

  // 4. HEARTBEAT KIỂM TRA PHIÊN 3 GIÂY/LẦN
  useEffect(() => {
    if (!isAuthenticated) return;

    const checkInterval = setInterval(async () => {
      const activePhone = localStorage.getItem('wellmax_current_phone') || currentUser?.phone;
      const activeFp = localStorage.getItem('wellmax_pass_fingerprint') || passwordFingerprint;

      if (!activePhone) return;

      try {
        const res = await fetch(`${API_BASE}/api/auth/check-session?phone=${encodeURIComponent(activePhone)}&fingerprint=${encodeURIComponent(activeFp)}`);
        if (res.ok) {
          const data = await res.json();
          if (data.valid === false) {
            triggerForceLogout(`🔒 ${data.reason || 'Phiên làm việc đã bị thu hồi!'}`);
          }
        }
      } catch (e) {}
    }, 3000);

    return () => clearInterval(checkInterval);
  }, [isAuthenticated, currentUser?.phone, passwordFingerprint]);

  // 5. KẾT NỐI SIGNALR & NHẬN TIN NHẮN REALTIME (KÈM ÂM BÁO & UNREAD BADGE)
  useEffect(() => {
    const currentChanId = String(activeGroupId).toLowerCase();

    signalRService.startConnection().then(() => {
      signalRService.joinChannel(currentChanId);
    });

    signalRService.onReceiveMessage((msg) => {
      const targetChanId = String(msg.channelId || '').toLowerCase();
      const isCurrentChannel = targetChanId === String(activeGroupId).toLowerCase();

      const formattedMsg = {
        id: msg.id || `msg-${Date.now()}`,
        channelId: targetChanId,
        senderId: msg.senderId,
        senderName: msg.senderName,
        senderAvatar: msg.senderAvatar,
        isMe: String(msg.senderId).toLowerCase() === String(currentUser?.id).toLowerCase(),
        isBot: msg.isBot,
        isSystem: msg.isSystem,
        text: msg.text,
        image: msg.imageUrl,
        file: msg.fileUrl ? { name: msg.fileName || 'Tài liệu', size: msg.fileSize || '1 MB', url: msg.fileUrl } : null,
        reminder: msg.reminderJson ? (typeof msg.reminderJson === 'string' ? JSON.parse(msg.reminderJson) : msg.reminderJson) : null,
        createdAt: msg.createdAtUnix || Date.now(),
        timestamp: new Date(msg.createdAtUnix || Date.now()).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        reactions: msg.reactions || {},
        isDeleted: msg.isDeleted
      };

      // Cập nhật messages
      setMessages(prev => {
        const currentList = prev[targetChanId] || [];
        const exists = currentList.some(m => 
          m.id === formattedMsg.id || 
          (m.isMe && m.text === formattedMsg.text && Math.abs((m.createdAt || 0) - (formattedMsg.createdAt || 0)) < 4000)
        );

        if (exists) {
          return {
            ...prev,
            [targetChanId]: currentList.map(m => (m.isMe && m.text === formattedMsg.text) ? formattedMsg : m)
          };
        }

        return {
          ...prev,
          [targetChanId]: [...currentList, formattedMsg]
        };
      });

      // Cập nhật lastMessage ở thanh bên trái
      setGroups(prev => prev.map(g => {
        if (String(g.id).toLowerCase() === targetChanId) {
          return {
            ...g,
            lastMessage: msg.text || (msg.imageUrl ? '[Hình ảnh]' : (msg.fileUrl ? '[Tệp tin]' : 'Tin nhắn mới')),
            lastTime: formattedMsg.timestamp
          };
        }
        return g;
      }));

      // Nếu tin nhắn gửi đến kênh người dùng KHÔNG ĐANG MỞ -> TĂNG SỐ LƯỢNG TIN NHẮN CHƯA ĐỌC (UNREAD)
      if (!isCurrentChannel && !formattedMsg.isMe) {
        setUnreadCounts(prev => ({
          ...prev,
          [targetChanId]: (prev[targetChanId] || 0) + 1
        }));
      }

      // Phát âm thanh thông báo: Kiểm tra xem người dùng có được @tag tên hoặc @All không
      if (!formattedMsg.isMe) {
        const isMentioned = (formattedMsg.text || '').includes('@All') || 
                            (currentUser?.name && (formattedMsg.text || '').includes(`@${currentUser.name}`));
        playNotificationSound(isMentioned, isSoundEnabled);
      }
    });

    // Lắng nghe cập nhật cảm xúc
    signalRService.onReactionUpdated((data) => {
      const chanId = String(data.channelId).toLowerCase();
      setMessages(prev => {
        const list = prev[chanId] || [];
        return {
          ...prev,
          [chanId]: list.map(m => m.id === data.messageId ? { ...m, reactions: data.reactions } : m)
        };
      });
    });

    // Lắng nghe cập nhật danh sách thành viên kênh (khi thêm hoặc xóa thành viên)
    signalRService.onChannelMembersUpdated((data) => {
      const chanId = String(data.channelId).toLowerCase();
      setGroups(prev => prev.map(g => {
        if (String(g.id).toLowerCase() === chanId) {
          return { ...g, members: data.members };
        }
        return g;
      }));
    });

    signalRService.onOrderExtracted((order) => {
      const orderChanId = String(order.channelId || '').toLowerCase();
      setChannelOrders(prev => ({
        ...prev,
        [orderChanId]: [order, ...(prev[orderChanId] || [])]
      }));
      setIsRightPanelOpen(true);
      setRightPanelTab('orders');
    });

    signalRService.onMessageRevoked((msgId) => {
      setMessages(prev => {
        const list = prev[currentChanId] || [];
        return {
          ...prev,
          [currentChanId]: list.map(m => m.id === msgId ? { ...m, isDeleted: true, text: 'Tin nhắn đã được thu hồi', image: null, file: null, reminder: null, reactions: {} } : m)
        };
      });
    });

    signalRService.onForceLogout((data) => {
      const myPhone = String(localStorage.getItem('wellmax_current_phone') || currentUser?.phone || '').trim();
      const myId = String(localStorage.getItem('wellmax_current_uid') || currentUserId).toLowerCase();

      const targetPhone = String(data?.phoneNumber || '').trim();
      const targetId = String(data?.userId || '').toLowerCase();

      if ((targetPhone && targetPhone === myPhone) || (targetId && targetId === myId)) {
        triggerForceLogout('🔒 Mật khẩu tài khoản của bạn vừa được thay đổi bởi Quản trị viên từ thiết bị khác. Phiên làm việc này đã bị ép đăng xuất ngay lập tức!');
      }
    });

    return () => {
      signalRService.leaveChannel(currentChanId);
    };
  }, [activeGroupId, currentUserId, currentUser?.phone, currentUser?.name, currentUser?.id, isSoundEnabled]);

  // Current Group Members
  const currentGroupMembers = useMemo(() => {
    const memberIds = activeGroup.members || [activeGroup.ownerId];
    return memberIds.map(mid => {
      const user = users.find(u => String(u.id).toLowerCase() === String(mid).toLowerCase()) || {
        id: mid,
        name: 'Người dùng',
        avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=80&q=80'
      };

      let roleType = 'member';
      let roleLabel = 'Thành viên';

      if (user.isBot || mid === '99999999-9999-9999-9999-999999999999') {
        roleType = 'bot';
        roleLabel = 'AI System';
      } else if (String(activeGroup.ownerId).toLowerCase() === String(mid).toLowerCase()) {
        roleType = 'owner';
        roleLabel = 'Trưởng kênh';
      } else if ((activeGroup.deputyIds || []).map(d => String(d).toLowerCase()).includes(String(mid).toLowerCase())) {
        roleType = 'deputy';
        roleLabel = 'Phó kênh';
      }

      return { ...user, roleType, roleLabel };
    });
  }, [activeGroup, users]);

  const pinnedMessages = useMemo(() => {
    const pinnedIds = activeGroup.pinnedMessageIds || [];
    return activeMessages.filter(m => pinnedIds.includes(m.id) && !m.isDeleted);
  }, [activeGroup.pinnedMessageIds, activeMessages]);

  const groupRemindersList = useMemo(() => {
    return activeMessages.filter(m => !!m.reminder && !m.isDeleted).map(m => ({
      msgId: m.id,
      senderName: m.senderName,
      timestamp: m.timestamp,
      ...m.reminder
    }));
  }, [activeMessages]);

  const groupMediaList = useMemo(() => {
    return activeMessages.filter(m => !!m.image && !m.isDeleted).map(m => ({
      id: m.id,
      url: m.image,
      senderName: m.senderName,
      timestamp: m.timestamp
    }));
  }, [activeMessages]);

  const groupFilesList = useMemo(() => {
    return activeMessages.filter(m => !!m.file && !m.isDeleted).map(m => ({
      id: m.id,
      file: m.file,
      senderName: m.senderName,
      timestamp: m.timestamp
    }));
  }, [activeMessages]);

  const groupLinksList = useMemo(() => {
    const list = [];
    const urlRegex = /(https?:\/\/[^\s]+)/g;
    activeMessages.forEach(m => {
      if (!m.isDeleted && m.text) {
        const urls = m.text.match(urlRegex) || [];
        urls.forEach(url => list.push({ id: `${m.id}-${url}`, url, senderName: m.senderName, timestamp: m.timestamp }));
      }
    });
    return list;
  }, [activeMessages]);

  // =========================================================================
  // GỬI TIN NHẮN (OPTIMISTIC UI 0MS + LƯU DATABASE POSTGRESQL + SIGNALR)
  // =========================================================================
  const handleSendMessage = async () => {
    if (!inputText.trim() && !selectedImagePreview && !selectedAttachedFile) return;

    const currentText = inputText.trim();
    const currentImg = selectedImagePreview;
    const currentFile = selectedAttachedFile;
    const targetChanId = String(activeGroupId).toLowerCase();

    setInputText('');
    setSelectedImagePreview(null);
    setSelectedAttachedFile(null);

    // 1. Optimistic UI: Hiển thị ngay tức thì 0ms trên màn hình người gửi
    const now = new Date();
    const tempMsgId = `temp-${Date.now()}`;
    const optimisticMsg = {
      id: tempMsgId,
      channelId: targetChanId,
      senderId: currentUser.id,
      senderName: currentUser.name,
      senderAvatar: currentUser.avatar,
      isMe: true,
      text: currentText,
      image: currentImg,
      file: currentFile,
      createdAt: Date.now(),
      timestamp: `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`,
      reactions: {},
      isDeleted: false
    };

    setMessages(prev => ({
      ...prev,
      [targetChanId]: [...(prev[targetChanId] || []), optimisticMsg]
    }));

    // Cập nhật lastMessage cho kênh
    setGroups(prev => prev.map(g => {
      if (String(g.id).toLowerCase() === targetChanId) {
        return {
          ...g,
          lastMessage: currentText || (currentImg ? '[Hình ảnh]' : '[Tệp tin]'),
          lastTime: optimisticMsg.timestamp
        };
      }
      return g;
    }));

    // 2. Gửi request về server lưu PostgreSQL và phát SignalR
    const payload = {
      channelId: activeGroupId,
      senderId: currentUser.id,
      senderName: currentUser.name,
      senderAvatar: currentUser.avatar,
      text: currentText,
      imageUrl: currentImg,
      fileUrl: currentFile?.url || null,
      fileName: currentFile?.name || null,
      fileSize: currentFile?.size || null,
      isBot: false,
      isSystem: false
    };

    try {
      const res = await fetch(`${API_BASE}/api/messages?geminiKey=${encodeURIComponent(llmConfig.apiKey || '')}&model=${llmConfig.modelName}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (res.ok) {
        const savedMsg = await res.json();
        setMessages(prev => {
          const list = prev[targetChanId] || [];
          return {
            ...prev,
            [targetChanId]: list.map(m => m.id === tempMsgId ? { ...m, id: savedMsg.id } : m)
          };
        });
      }
    } catch (e) {
      console.warn("Backend warning:", e);
    }
  };

  // =========================================================================
  // THU HỒI TIN NHẮN (CHỈ ÁP DỤNG TRONG VÒNG 24H)
  // =========================================================================
  const handleRevokeMessage = async (msgId) => {
    try {
      const res = await fetch(`${API_BASE}/api/messages/${msgId}/revoke?senderId=${encodeURIComponent(currentUser.id)}`, {
        method: 'DELETE'
      });
      if (res.ok) {
        const currentChanId = String(activeGroupId).toLowerCase();
        setMessages(prev => {
          const list = prev[currentChanId] || [];
          return {
            ...prev,
            [currentChanId]: list.map(m => m.id === msgId ? { ...m, isDeleted: true, text: 'Tin nhắn đã được thu hồi', image: null, file: null, reminder: null, reactions: {} } : m)
          };
        });
      } else {
        const err = await res.json().catch(() => ({}));
        alert(err.message || "Không thể thu hồi tin nhắn.");
      }
    } catch (e) {
      alert("Lỗi khi kết nối máy chủ để thu hồi tin nhắn.");
    }
    setDeleteMsgConfirmData(null);
  };

  // =========================================================================
  // GHIM / BỎ GHIM TIN NHẮN (TỐI ĐA 3 TIN)
  // =========================================================================
  const handleTogglePinMessage = (msgId) => {
    const currentPinned = activeGroup.pinnedMessageIds || [];
    const isAlreadyPinned = currentPinned.includes(msgId);
    let updated;
    if (isAlreadyPinned) {
      updated = currentPinned.filter(id => id !== msgId);
    } else {
      // Giới hạn tối đa 3 tin nhắn được ghim
      updated = [...currentPinned.slice(-2), msgId];
    }
    setGroups(prev => prev.map(g => String(g.id).toLowerCase() === String(activeGroupId).toLowerCase() ? { ...g, pinnedMessageIds: updated } : g));
  };

  // =========================================================================
  // THẢ CẢM XÚC / THẢ TIM (LIKE, HEART, HAHA, WOW, SAD, ANGRY)
  // =========================================================================
  const handleToggleReaction = async (msgId, rxType) => {
    try {
      await fetch(`${API_BASE}/api/messages/${msgId}/reaction`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reactionType: rxType, userId: currentUser.id })
      });
    } catch (e) {}

    // Cập nhật lạc quan trên UI
    const currentChanId = String(activeGroupId).toLowerCase();
    setMessages(prev => {
      const list = prev[currentChanId] || [];
      return {
        ...prev,
        [currentChanId]: list.map(m => {
          if (m.id === msgId) {
            const rx = { ...(m.reactions || {}) };
            rx[rxType] = (rx[rxType] || 0) + 1;
            return { ...m, reactions: rx };
          }
          return m;
        })
      };
    });
  };

  // =========================================================================
  // TẠO NHẮC HẸN VẬN HÀNH
  // =========================================================================
  const handleCreateReminder = async (reminderData) => {
    const payload = {
      channelId: activeGroupId,
      senderId: currentUser.id,
      senderName: currentUser.name,
      senderAvatar: currentUser.avatar,
      text: `📅 Lịch hẹn mới: ${reminderData.title}`,
      reminderJson: JSON.stringify({ ...reminderData, participants: [currentUser.id], status: 'upcoming' }),
      isBot: false,
      isSystem: false
    };

    try {
      await fetch(`${API_BASE}/api/messages`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
    } catch (e) {}

    setIsReminderModalOpen(false);
  };

  // TẠO KÊNH BÁN HÀNG MỚI LƯU DATABASE
  const handleCreateChannel = async ({ name, description, avatar }) => {
    try {
      const res = await fetch(`${API_BASE}/api/channels`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          description,
          avatar,
          ownerId: currentUser.id
        })
      });

      if (res.ok) {
        const createdChannel = await res.json();
        setGroups(prev => [createdChannel, ...prev]);
        setActiveGroupId(createdChannel.id);
        fetchMessagesForChannel(createdChannel.id);
        alert(`Đã tạo kênh "${name}" thành công vào Database!`);
      }
    } catch (e) {
      alert("Lỗi khi tạo kênh trên server.");
    }
    setIsCreateChannelOpen(false);
  };

  // THÊM THÀNH VIÊN VÀO KÊNH BÁN HÀNG LƯU DATABASE & PHÁT REALTIME
  const handleAddMemberToChannel = async (userId) => {
    try {
      const res = await fetch(`${API_BASE}/api/channels/${activeGroupId}/members`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId })
      });

      if (res.ok) {
        const data = await res.json();
        setGroups(prev => prev.map(g => {
          if (String(g.id).toLowerCase() === String(activeGroupId).toLowerCase()) {
            return { ...g, members: data.members };
          }
          return g;
        }));
        alert(data.message || "Đã thêm thành viên vào kênh thành công!");
      } else {
        const err = await res.json().catch(() => ({}));
        alert(err.message || "Không thể thêm thành viên vào kênh.");
      }
    } catch (e) {
      alert("Lỗi kết nối máy chủ khi thêm thành viên.");
    }
    setIsAddMemberOpen(false);
  };

  // TRƯỞNG NHÓM XÓA THÀNH VIÊN KHỎI KÊNH BÁN HÀNG LƯU DATABASE & PHÁT REALTIME
  const handleRemoveMemberFromChannel = async (memberId) => {
    try {
      const res = await fetch(`${API_BASE}/api/channels/${activeGroupId}/members/${memberId}?requesterId=${encodeURIComponent(currentUser.id)}`, {
        method: 'DELETE'
      });

      if (res.ok) {
        const data = await res.json();
        setGroups(prev => prev.map(g => {
          if (String(g.id).toLowerCase() === String(activeGroupId).toLowerCase()) {
            return { ...g, members: data.members };
          }
          return g;
        }));
        alert(data.message || "Đã xóa thành viên khỏi kênh!");
      } else {
        const err = await res.json().catch(() => ({}));
        alert(err.message || "Bạn không có quyền xóa thành viên này.");
      }
    } catch (e) {
      alert("Lỗi kết nối khi xóa thành viên.");
    }
  };

  const handleSaveProfile = async (profileData) => {
    try {
      await fetch(`${API_BASE}/api/auth/profile`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userId: String(currentUser.id),
          phone: currentUser.phone,
          name: profileData.name,
          avatar: profileData.avatar,
          bio: profileData.bio
        })
      });
    } catch (e) {}

    setUsers(prev => {
      const updated = prev.map(u => {
        if (String(u.id).toLowerCase() === String(currentUser.id).toLowerCase()) {
          return { ...u, ...profileData };
        }
        return u;
      });
      localStorage.setItem('wellmax_users', JSON.stringify(updated));
      return updated;
    });

    setIsProfileModalOpen(false);
    fetchUsersFromDb();
    alert("Đã cập nhật hồ sơ cá nhân thành công!");
  };

  const handleSaveUserForm = async (formData) => {
    const selectedRoleMeta = ROLE_GROUP_OPTIONS.find(r => r.name === formData.roleGroup);
    const isAdminRole = selectedRoleMeta ? selectedRoleMeta.isAdmin : false;

    try {
      if (editingUser) {
        await fetch(`${API_BASE}/api/auth/users/${editingUser.id}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            name: formData.name,
            phone: formData.phone,
            birthDate: formData.birthDate,
            gender: formData.gender,
            department: formData.department,
            roleGroup: formData.roleGroup,
            avatar: formData.avatar,
            note: formData.note,
            isAdmin: isAdminRole,
            isActive: formData.isActive
          })
        });
      } else {
        await fetch(`${API_BASE}/api/auth/users`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            name: formData.name,
            phone: formData.phone,
            password: formData.password || '123',
            birthDate: formData.birthDate,
            gender: formData.gender,
            department: formData.department,
            roleGroup: formData.roleGroup,
            avatar: formData.avatar,
            note: formData.note,
            isAdmin: isAdminRole,
            isActive: formData.isActive
          })
        });
      }

      fetchUsersFromDb();
      alert(editingUser ? "Đã cập nhật thông tin nhân sự thành công!" : "Đã tạo nhân sự mới thành công!");
    } catch (e) {
      alert("Có lỗi xảy ra khi lưu nhân sự.");
    }

    setIsUserFormOpen(false);
    setEditingUser(null);
  };

  const handleToggleUserActive = async (targetUser) => {
    if (String(targetUser.id).toLowerCase() === String(currentUser.id).toLowerCase()) {
      alert("Không thể tự khóa tài khoản bạn đang đăng nhập!");
      return;
    }

    try {
      const res = await fetch(`${API_BASE}/api/auth/users/${targetUser.id}/toggle-active`, {
        method: 'PUT'
      });
      if (res.ok) {
        fetchUsersFromDb();
      }
    } catch (e) {
      setUsers(prev => prev.map(x => x.id === targetUser.id ? { ...x, isActive: !x.isActive } : x));
    }
  };

  const handleSavePassword = async (targetUserObj, newPassword) => {
    const targetUserId = targetUserObj.id;
    const targetUserPhone = targetUserObj.phone;
    const targetUserName = targetUserObj.name;

    const myPhone = String(localStorage.getItem('wellmax_current_phone') || currentUser?.phone || '').trim();
    const myId = String(localStorage.getItem('wellmax_current_uid') || currentUserId).toLowerCase();

    const isChangingMyself = (targetUserPhone && targetUserPhone === myPhone) || (String(targetUserId).toLowerCase() === myId);

    try {
      const res = await fetch(`${API_BASE}/api/auth/change-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ 
          userId: String(targetUserId), 
          phoneNumber: targetUserPhone,
          newPassword 
        })
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.message || 'Lỗi khi cập nhật database');
      }
    } catch (e) {
      console.warn("Backend API call warning:", e);
    }

    setUsers(prev => {
      const updated = prev.map(u => {
        if (String(u.id).toLowerCase() === String(targetUserId).toLowerCase() || (targetUserPhone && u.phone === targetUserPhone)) {
          return { ...u, password: newPassword };
        }
        return u;
      });
      localStorage.setItem('wellmax_users', JSON.stringify(updated));
      return updated;
    });

    setIsChangePasswordOpen(false);
    setTargetPasswordUser(null);

    if (isChangingMyself) {
      triggerForceLogout(`🔒 Mật khẩu của bạn (${targetUserName}) đã được đổi thành công!\n\nPhiên làm việc này bị thu hồi. Vui lòng đăng nhập lại với mật khẩu mới.`);
    } else {
      alert(`✅ Đã đổi mật khẩu cho nhân sự "${targetUserName}" (${targetUserPhone}) thành công!\n\nLệnh thu hồi phiên đã được phát qua máy chủ: Bất kỳ trình duyệt hay máy tính nào đang mở tài khoản này sẽ tự động bị đóng phiên ngay lập tức.`);
    }
  };

  const handleLoginSuccess = (userId, userData) => {
    setCurrentUserId(userId);
    const phone = userData?.phone || userData?.phoneNumber || '0988888888';
    const fp = userData?.passwordFingerprint || '';
    
    setCurrentUserPhone(phone);
    setPasswordFingerprint(fp);

    setIsAuthenticated(true);
    localStorage.setItem('wellmax_auth', 'true');
    localStorage.setItem('wellmax_current_uid', userId);
    localStorage.setItem('wellmax_current_phone', phone);
    localStorage.setItem('wellmax_pass_fingerprint', fp);

    if (userData) {
      setUsers(prev => {
        const exists = prev.find(u => String(u.id).toLowerCase() === String(userData.id).toLowerCase());
        if (!exists) return [userData, ...prev];
        return prev.map(u => String(u.id).toLowerCase() === String(userData.id).toLowerCase() ? { ...u, ...userData } : u);
      });
    }
  };

  const handleLogout = () => {
    triggerForceLogout();
  };

  if (!isAuthenticated) {
    return <AuthLoginView users={users} onLoginSuccess={handleLoginSuccess} />;
  }

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-[#f0f4f5] text-slate-800 font-sans antialiased">
      {/* 1. Left Nav */}
      <LeftNavigation 
        currentUser={currentUser}
        isSystemAdmin={isSystemAdmin}
        isSoundEnabled={isSoundEnabled}
        onToggleSound={() => setIsSoundEnabled(!isSoundEnabled)}
        onOpenProfile={() => setIsProfileModalOpen(true)}
        onOpenCreateGroup={() => setIsCreateChannelOpen(true)}
        onOpenUserMgmt={() => setIsUserMgmtOpen(true)}
        onOpenAISettings={() => setIsAISettingsOpen(true)}
        onLogout={handleLogout}
      />

      {/* 2. Channel List (Kèm unread badge & tin nhắn cắt ngắn gọn) */}
      <ChannelListSidebar 
        groups={groups}
        activeGroupId={activeGroupId}
        searchKeyword={searchKeyword}
        setSearchKeyword={setSearchKeyword}
        groupViewTab={groupViewTab}
        setGroupViewTab={setGroupViewTab}
        unreadCounts={unreadCounts}
        onSelectGroup={(id) => {
          const cleanId = String(id).toLowerCase();
          setActiveGroupId(cleanId);
          // Đánh dấu đã đọc = 0 và ẩn huy hiệu số lượng đi như Zalo
          setUnreadCounts(prev => {
            const next = { ...prev };
            delete next[cleanId];
            return next;
          });
          fetchMessagesForChannel(cleanId);
        }}
        onOpenCreateGroup={() => setIsCreateChannelOpen(true)}
        onTogglePinGroup={async (id) => {
          try {
            await fetch(`${API_BASE}/api/channels/${id}/pin`, { method: 'PUT' });
            fetchChannelsFromDb();
          } catch (e) {
            setGroups(prev => prev.map(g => g.id === id ? { ...g, isPinned: !g.isPinned } : g));
          }
        }}
        onRestoreHiddenGroup={(id) => setGroups(prev => prev.map(g => g.id === id ? { ...g, isHidden: false } : g))}
      />

      {/* 3. Main Chat View */}
      <main className="flex-1 flex flex-col bg-[#f0f4f5] relative min-w-0">
        <header className="h-16 bg-white border-b border-slate-200 px-5 flex items-center justify-between shadow-xs z-10">
          <div className="flex items-center gap-3 min-w-0">
            <img src={activeGroup.avatar} alt={activeGroup.name} className="w-10 h-10 rounded-full object-cover border-2 border-slate-100" />
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <h3 className="font-bold text-[#164E5B] text-base truncate">{activeGroup.name}</h3>
                {activeGroup.isPinned && (
                  <span className="text-[11px] bg-[#98C93C]/15 text-[#6b941f] border border-[#98C93C]/40 px-1.5 py-0.2 rounded font-semibold flex items-center gap-1">
                    <Pin className="w-3 h-3 fill-[#86B828] text-[#86B828] rotate-45" /> Đã ghim
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-500 truncate">{currentGroupMembers.length} thành viên • {activeGroup.description}</p>
            </div>
          </div>

          <div className="flex items-center gap-1">
            <button onClick={() => setIsReminderModalOpen(true)} className="p-2 hover:bg-[#ebf4f6] text-slate-600 rounded-lg cursor-pointer" title="Lịch hẹn vận hành">
              <Calendar className="w-5 h-5" />
            </button>
            <button onClick={() => setIsRightPanelOpen(!isRightPanelOpen)} className="p-2 hover:bg-slate-100 text-slate-600 rounded-lg cursor-pointer" title="Ẩn/hiện thanh điều hành">
              {isRightPanelOpen ? <PanelRightClose className="w-5 h-5" /> : <PanelRightOpen className="w-5 h-5" />}
            </button>
          </div>
        </header>

        {/* Pin Banner (Hiển thị tối đa 3 tin nhắn ghim) */}
        {pinnedMessages.length > 0 && (
          <div className="bg-white border-b border-[#cce4e9] px-4 py-2 flex items-center justify-between text-xs">
            <div className="flex items-center gap-2 truncate">
              <Pin className="w-3.5 h-3.5 fill-[#98C93C] text-[#86B828] rotate-45" />
              <span className="font-bold text-[#164E5B]">Tin ghim ({pinnedMessages.length}/3):</span>
              <span className="text-slate-600 truncate">{pinnedMessages[0].text}</span>
            </div>
          </div>
        )}

        {/* Message Stream */}
        <MessageStream 
          messages={activeMessages}
          currentUser={currentUser}
          activeGroup={activeGroup}
          highlightedMsgId={null}
          messageRefs={messageRefs}
          messagesEndRef={messagesEndRef}
          isBotTyping={isBotTyping}
          onTogglePinMessage={handleTogglePinMessage}
          onOpenDeleteConfirm={(msg) => setDeleteMsgConfirmData(msg)}
          onToggleReaction={handleToggleReaction}
          onOpenLightbox={(url) => setLightboxImageUrl(url)}
        />

        {/* Input Bar */}
        <ChatInputBar 
          inputText={inputText}
          setInputText={setInputText}
          onSendMessage={handleSendMessage}
          selectedImagePreview={selectedImagePreview}
          setSelectedImagePreview={setSelectedImagePreview}
          selectedAttachedFile={selectedAttachedFile}
          setSelectedAttachedFile={setSelectedAttachedFile}
          currentGroupMembers={currentGroupMembers}
          onOpenReminderModal={() => setIsReminderModalOpen(true)}
          textareaRef={textareaRef}
        />
      </main>

      {/* 4. Right Operation Panel (Trưởng nhóm được quyền Xóa & Thêm thành viên) */}
      {isRightPanelOpen && (
        <RightOperationSidebar 
          activeGroup={activeGroup}
          currentUser={currentUser}
          currentGroupMembers={currentGroupMembers}
          channelOrders={channelOrders}
          rightPanelTab={rightPanelTab}
          setRightPanelTab={setRightPanelTab}
          isMembersSectionOpen={isMembersSectionOpen}
          setIsMembersSectionOpen={setIsMembersSectionOpen}
          onClose={() => setIsRightPanelOpen(false)}
          onOpenAddMember={() => setIsAddMemberOpen(true)}
          onRemoveMember={handleRemoveMemberFromChannel}
          onUpdateOrderStatus={(id, st) => {
            setChannelOrders(prev => ({
              ...prev,
              [String(activeGroupId).toLowerCase()]: (prev[String(activeGroupId).toLowerCase()] || []).map(o => o.id === id ? { ...o, status: st } : o)
            }));
          }}
          groupMediaList={groupMediaList}
          groupFilesList={groupFilesList}
          groupLinksList={groupLinksList}
          groupRemindersList={groupRemindersList}
          onOpenLightbox={(url) => setLightboxImageUrl(url)}
          onScrollToMessage={(id) => {}}
        />
      )}

      {/* Modals Container */}
      {isCreateChannelOpen && (
        <CreateChannelModal 
          onClose={() => setIsCreateChannelOpen(false)}
          onCreateChannel={handleCreateChannel}
        />
      )}

      {isAddMemberOpen && (
        <AddMemberModal 
          users={users}
          activeGroup={activeGroup}
          onClose={() => setIsAddMemberOpen(false)}
          onAddMember={handleAddMemberToChannel}
        />
      )}

      {isProfileModalOpen && (
        <ProfileModal 
          currentUser={currentUser}
          onClose={() => setIsProfileModalOpen(false)}
          onSaveProfile={handleSaveProfile}
        />
      )}

      {isUserMgmtOpen && (
        <UserManagementModal 
          users={users}
          currentUser={currentUser}
          onClose={() => setIsUserMgmtOpen(false)}
          onOpenCreateUser={() => {
            setEditingUser(null);
            setIsUserFormOpen(true);
          }}
          onOpenEditUser={(user) => {
            setEditingUser(user);
            setIsUserFormOpen(true);
          }}
          onToggleUserActive={handleToggleUserActive}
          onOpenChangePassword={(user) => {
            setTargetPasswordUser(user);
            setIsChangePasswordOpen(true);
          }}
        />
      )}

      {isUserFormOpen && (
        <UserFormModal 
          editingUser={editingUser}
          onClose={() => {
            setIsUserFormOpen(false);
            setEditingUser(null);
          }}
          onSaveUser={handleSaveUserForm}
        />
      )}

      {isChangePasswordOpen && targetPasswordUser && (
        <ChangePasswordModal 
          targetUser={targetPasswordUser}
          onClose={() => {
            setIsChangePasswordOpen(false);
            setTargetPasswordUser(null);
          }}
          onSavePassword={handleSavePassword}
        />
      )}

      {isAISettingsOpen && (
        <AISettingsModal 
          llmConfig={llmConfig}
          setLlmConfig={setLlmConfig}
          onClose={() => setIsAISettingsOpen(false)}
        />
      )}

      {isReminderModalOpen && (
        <ReminderModal 
          onClose={() => setIsReminderModalOpen(false)}
          onCreateReminder={handleCreateReminder}
        />
      )}

      {/* Popup xác nhận thu hồi tin nhắn trong vòng 24h */}
      {deleteMsgConfirmData && (
        <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4 backdrop-blur-xs">
          <div className="bg-white rounded-2xl w-full max-w-sm p-5 space-y-3 shadow-2xl animate-in zoom-in-95">
            <h3 className="font-bold text-base text-rose-600">Thu hồi tin nhắn</h3>
            <p className="text-xs text-slate-600 leading-relaxed">
              Bạn có chắc chắn muốn thu hồi tin nhắn này? Tin nhắn sẽ được xóa bỏ khỏi giao diện của mọi thành viên trong kênh.
            </p>
            <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
              <button 
                onClick={() => setDeleteMsgConfirmData(null)} 
                className="px-3.5 py-1.5 text-xs text-slate-600 hover:bg-slate-100 rounded-lg cursor-pointer"
              >
                Hủy
              </button>
              <button 
                onClick={() => handleRevokeMessage(deleteMsgConfirmData.id)} 
                className="px-4 py-1.5 bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold rounded-lg shadow-xs cursor-pointer"
              >
                Xác nhận thu hồi
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Lightbox Modal phóng to ảnh */}
      {lightboxImageUrl && (
        <div 
          onClick={() => setLightboxImageUrl(null)} 
          className="fixed inset-0 bg-black/85 z-50 flex items-center justify-center p-4 cursor-zoom-out backdrop-blur-sm animate-in fade-in"
        >
          <img src={lightboxImageUrl} alt="Phóng to" className="max-w-full max-h-[88vh] rounded-xl shadow-2xl object-contain" />
        </div>
      )}
    </div>
  );
}