export const INITIAL_SYSTEM_USERS = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Đào Hà (Admin)',
    phone: '0988888888',
    password: '123',
    birthDate: '1990-05-15',
    gender: 'Nam',
    department: 'Ban Giám Đốc',
    roleGroup: 'Quản trị viên tối cao (Super Admin)',
    avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=80&q=80',
    note: 'Sáng lập viên & Quản trị trưởng hệ thống',
    bio: 'Quản trị viên & Điều hành kinh doanh',
    isAdmin: true,
    isActive: true
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    name: 'Lê Hoàng Nam',
    phone: '0912345671',
    password: '123',
    birthDate: '1992-08-20',
    gender: 'Nam',
    department: 'Vận Hành & Kho',
    roleGroup: 'Quản lý bộ phận (Manager)',
    avatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=80&q=80',
    note: 'Phụ trách điều phối kho và giao vận hỏa tốc',
    bio: 'Trưởng Nhóm Vận Hành & Kho',
    isAdmin: false,
    isActive: true
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    name: 'Phạm Thu Trang',
    phone: '0912345672',
    password: '123',
    birthDate: '1996-11-04',
    gender: 'Nữ',
    department: 'Kinh Doanh & Sale',
    roleGroup: 'Nhân viên kinh doanh (Sale Rep)',
    avatar: 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=80&q=80',
    note: 'Chuyên chốt đơn livestream và khách VIP TikTok',
    bio: 'Chuyên Viên Chốt Đơn TikTok',
    isAdmin: false,
    isActive: true
  },
  {
    id: '44444444-4444-4444-4444-444444444444',
    name: 'Đặng Tuấn Kiệt',
    phone: '0912345673',
    password: '123',
    birthDate: '1993-02-12',
    gender: 'Nam',
    department: 'Kế Toán - Tài Chính',
    roleGroup: 'Kế toán - Công nợ (Finance)',
    avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=80&q=80',
    note: 'Quản lý sổ quỹ, hóa đơn VAT và hạn mức công nợ',
    bio: 'Kế Toán & Quản Lý Công Nợ',
    isAdmin: false,
    isActive: true
  },
  {
    id: '55555555-5555-5555-5555-555555555555',
    name: 'Nguyễn Bích Ngọc',
    phone: '0912345674',
    password: '123',
    birthDate: '1995-09-30',
    gender: 'Nữ',
    department: 'Chăm Sóc Khách Hàng',
    roleGroup: 'Chuyên viên CSKH (Support)',
    avatar: 'https://images.unsplash.com/photo-1517841905240-472988babdf9?auto=format&fit=crop&w=80&q=80',
    note: 'Tư vấn phác đồ da và chăm sóc sắc đẹp',
    bio: 'Chuyên Viên Tư Vấn Facebook Ads',
    isAdmin: false,
    isActive: true
  },
  {
    id: '99999999-9999-9999-9999-999999999999',
    name: 'Wellmax Brain AI System',
    phone: '19008888',
    password: '',
    birthDate: '2026-01-01',
    gender: 'Khác',
    department: 'Kỹ Thuật & AI',
    roleGroup: 'Quản trị viên tối cao (Super Admin)',
    avatar: 'bot-avatar',
    note: 'AI Agent trung tâm điều hành dữ liệu và trích xuất đơn hàng',
    bio: 'Trợ lý điều hành kinh doanh & đơn hàng',
    isAdmin: false,
    isActive: true,
    isBot: true
  }
];

export const INITIAL_GROUPS = [
  {
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    name: 'Kênh TikTok Shop & Livestream',
    avatar: 'https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=120&q=80',
    description: 'Tiếp nhận đơn hàng livestream và tư vấn qua TikTok Shop',
    ownerId: '11111111-1111-1111-1111-111111111111',
    deputyIds: ['22222222-2222-2222-2222-222222222222'],
    members: [
      '11111111-1111-1111-1111-111111111111',
      '22222222-2222-2222-2222-222222222222',
      '33333333-3333-3333-3333-333333333333',
      '99999999-9999-9999-9999-999999999999'
    ],
    isPinned: true,
    pinnedAt: Date.now(),
    isHidden: false,
    lastMessage: 'Chưa có tin nhắn mới',
    lastTime: ''
  },
  {
    id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    name: 'Kênh Facebook Ads & Fanpage',
    avatar: 'https://images.unsplash.com/photo-1497366216548-37526070297c?auto=format&fit=crop&w=120&q=80',
    description: 'Xử lý inbox khách hàng từ chiến dịch quảng cáo Facebook',
    ownerId: '22222222-2222-2222-2222-222222222222',
    deputyIds: [],
    members: [
      '22222222-2222-2222-2222-222222222222',
      '55555555-5555-5555-5555-555555555555',
      '99999999-9999-9999-9999-999999999999'
    ],
    isPinned: false,
    pinnedAt: 0,
    isHidden: false,
    lastMessage: 'Chưa có tin nhắn mới',
    lastTime: ''
  },
  {
    id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    name: 'Kênh Đại Lý & Bán Buôn B2B',
    avatar: 'https://images.unsplash.com/photo-1556761175-5973dc0f32e7?auto=format&fit=crop&w=120&q=80',
    description: 'Điều phối xuất kho đại lý và nhà phân phối toàn quốc',
    ownerId: '55555555-5555-5555-5555-555555555555',
    deputyIds: ['11111111-1111-1111-1111-111111111111'],
    members: [
      '55555555-5555-5555-5555-555555555555',
      '11111111-1111-1111-1111-111111111111',
      '44444444-4444-4444-4444-444444444444',
      '99999999-9999-9999-9999-999999999999'
    ],
    isPinned: false,
    pinnedAt: 0,
    isHidden: false,
    lastMessage: 'Chưa có tin nhắn mới',
    lastTime: ''
  }
];

// XÓA TOÀN BỘ TIN NHẮN DEMO: Ban đầu không có tin nhắn rác
export const INITIAL_MESSAGES = {};
export const INITIAL_CHANNEL_ORDERS = {};

export const DEPARTMENT_OPTIONS = [
  'Ban Giám Đốc',
  'Kinh Doanh & Sale',
  'Vận Hành & Kho',
  'Kế Toán - Tài Chính',
  'Chăm Sóc Khách Hàng',
  'Kỹ Thuật & AI'
];

export const ROLE_GROUP_OPTIONS = [
  { id: 'role-1', name: 'Quản trị viên tối cao (Super Admin)', isAdmin: true },
  { id: 'role-2', name: 'Quản lý bộ phận (Manager)', isAdmin: false },
  { id: 'role-3', name: 'Nhân viên kinh doanh (Sale Rep)', isAdmin: false },
  { id: 'role-4', name: 'Kế toán - Công nợ (Finance)', isAdmin: false },
  { id: 'role-5', name: 'Chuyên viên CSKH (Support)', isAdmin: false }
];

export const REACTION_EMOJIS = [
  { id: 'like', icon: '👍', name: 'Thích' },
  { id: 'heart', icon: '❤️', name: 'Yêu thích' },
  { id: 'haha', icon: '😄', name: 'Cười' },
  { id: 'wow', icon: '😮', name: 'Bất ngờ' },
  { id: 'sad', icon: '😢', name: 'Buồn' },
  { id: 'angry', icon: '😡', name: 'Phẫn nộ' }
];