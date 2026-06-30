# Sơ đồ Use Case người chơi — include và extend

Sơ đồ này mở rộng trực tiếp các use case trong hình ban đầu (`Join Game`, `Create Game`, `Move`, `Aim`, `Shoot`, `Reload`, `Pickup Weapon`) theo chương trình hiện tại.

```mermaid
flowchart LR
    PLAYER["👤<br/>NGƯỜI CHƠI"]

    subgraph SYSTEM["HỆ THỐNG TOP DOWN SHOOTER 3D"]
        direction TB

        COOP(["Chơi Co-op LAN"])
        ROOM(["Vào phòng chơi"])
        CREATE(["Create Game<br/>Tạo phòng / làm Host"])
        JOIN(["Join Game<br/>Tham gia phòng"])
        NAME(["Nhập tên hiển thị"])
        BROWSE(["Xem và làm mới danh sách phòng"])
        SELECT_ROOM(["Chọn phòng"])
        READY(["Sẵn sàng"])

        SETUP(["Thiết lập trận chơi"])
        HOST_MISSION(["Host chọn nhiệm vụ"])
        LOADOUT(["Mỗi người chọn tối đa 2 vũ khí"])
        COMIC(["Xem truyện mở đầu"])
        HOST_START(["Host bắt đầu trận"])

        COMBAT(["Tham gia chiến đấu"])
        MOVE(["Move<br/>Di chuyển"])
        RUN(["Chạy"])
        ATTACK(["Tấn công kẻ địch"])
        AIM(["Aim<br/>Ngắm bắn"])
        SHOOT(["Shoot<br/>Bắn"])
        WEAPON(["Quản lý vũ khí"])
        SWITCH(["Đổi vũ khí"])
        RELOAD(["Reload<br/>Nạp đạn"])
        INTERACT(["Tương tác vật phẩm"])
        PICK_WEAPON(["Pickup Weapon<br/>Nhặt vũ khí"])
        PICK_AMMO(["Nhặt đạn"])
        DRIVE(["Sử dụng phương tiện"])
        PAUSE(["Tạm dừng / cài đặt"])

        MISSION(["Thực hiện nhiệm vụ"])
        SURVIVE(["Sống sót theo thời gian"])
        FIND_KEY(["Tìm chìa khóa"])
        HUNT(["Săn mục tiêu"])
        DEFEND(["Phòng thủ vị trí"])
        DELIVER(["Giao xe"])
        EXTRACT(["Đến điểm thoát"])

        RESULT(["Xử lý kết quả trận"])
        VIEW_RESULT(["Xem kết quả"])
        VICTORY(["Xem chiến thắng"])
        GAME_OVER(["Xem Game Over"])
        REPLAY(["Chơi lại"])
        MENU(["Trở về menu"])

        COOP -. "«include»" .-> ROOM
        CREATE -. "«extend»<br/>[chọn làm Host]" .-> ROOM
        JOIN -. "«extend»<br/>[chọn tham gia]" .-> ROOM
        ROOM -. "«include»" .-> NAME
        JOIN -. "«include»" .-> BROWSE
        JOIN -. "«include»" .-> SELECT_ROOM
        COOP -. "«include»" .-> READY
        COOP -. "«include»" .-> SETUP
        COOP -. "«include»" .-> COMBAT

        HOST_MISSION -. "«extend»<br/>[người chơi là Host]" .-> SETUP
        SETUP -. "«include»" .-> LOADOUT
        SETUP -. "«include»" .-> COMIC
        HOST_START -. "«extend»<br/>[Host và mọi người đã sẵn sàng]" .-> SETUP

        COMBAT -. "«include»" .-> MOVE
        RUN -. "«extend»<br/>[giữ phím chạy]" .-> MOVE
        COMBAT -. "«include»" .-> ATTACK
        ATTACK -. "«include»" .-> AIM
        ATTACK -. "«include»" .-> SHOOT
        COMBAT -. "«include»" .-> WEAPON
        SWITCH -. "«extend»<br/>[có vũ khí ở khe khác]" .-> WEAPON
        RELOAD -. "«extend»<br/>[băng chưa đầy và còn đạn dự trữ]" .-> WEAPON
        INTERACT -. "«extend»<br/>[ở gần đối tượng tương tác]" .-> COMBAT
        PICK_WEAPON -. "«extend»<br/>[đối tượng là vũ khí]" .-> INTERACT
        PICK_AMMO -. "«extend»<br/>[đối tượng là hộp đạn]" .-> INTERACT
        DRIVE -. "«extend»<br/>[tương tác với xe]" .-> COMBAT
        PAUSE -. "«extend»<br/>[người chơi mở Pause]" .-> COMBAT
        COMBAT -. "«include»" .-> MISSION

        SURVIVE -. "«extend»<br/>[nhiệm vụ Timer]" .-> MISSION
        FIND_KEY -. "«extend»<br/>[nhiệm vụ Key Find]" .-> MISSION
        HUNT -. "«extend»<br/>[nhiệm vụ Enemy Hunt]" .-> MISSION
        DEFEND -. "«extend»<br/>[nhiệm vụ Last Defence]" .-> MISSION
        DELIVER -. "«extend»<br/>[nhiệm vụ Car Delivery]" .-> MISSION
        MISSION -. "«include»" .-> EXTRACT

        RESULT -. "«include»" .-> VIEW_RESULT
        VICTORY -. "«extend»<br/>[hoàn thành nhiệm vụ]" .-> VIEW_RESULT
        GAME_OVER -. "«extend»<br/>[người chơi/đội bị hạ]" .-> VIEW_RESULT
        REPLAY -. "«extend»<br/>[chọn chơi lại]" .-> RESULT
        MENU -. "«extend»<br/>[chọn về menu]" .-> RESULT
    end

    PLAYER --> COOP
    PLAYER --> COMBAT
    PLAYER --> RESULT

    classDef actor fill:#263238,color:#fff,stroke:#111,stroke-width:2px;
    classDef main fill:#1565c0,color:#fff,stroke:#0d47a1,stroke-width:2px;
    classDef detail fill:#e3f2fd,color:#102027,stroke:#42a5f5,stroke-width:1.5px;
    class PLAYER actor;
    class COOP,SETUP,COMBAT,MISSION,RESULT main;
    class ROOM,CREATE,JOIN,NAME,BROWSE,SELECT_ROOM,READY,HOST_MISSION,LOADOUT,COMIC,HOST_START,MOVE,RUN,ATTACK,AIM,SHOOT,WEAPON,SWITCH,RELOAD,INTERACT,PICK_WEAPON,PICK_AMMO,DRIVE,PAUSE,SURVIVE,FIND_KEY,HUNT,DEFEND,DELIVER,EXTRACT,VIEW_RESULT,VICTORY,GAME_OVER,REPLAY,MENU detail;
```

## Quy ước và lựa chọn mô hình

- `A «include» B`: B là hành vi con bắt buộc hoặc được A tái sử dụng; mũi tên đi từ A sang B.
- `A «extend» B`: A chỉ bổ sung cho B khi điều kiện trong ngoặc vuông xảy ra; mũi tên đi từ A sang B.
- `Create Game` và `Join Game` là hai nhánh thay thế của `Vào phòng chơi`; người chơi chỉ chọn một vai trò tại một thời điểm.
- `Reload` mở rộng `Quản lý vũ khí`, không mở rộng trực tiếp `Shoot`, vì code cho phép chủ động nạp đạn ngay cả khi chưa bắn.

## Căn cứ từ chương trình hiện tại

| Nhóm chức năng | Mã nguồn xác nhận |
|---|---|
| Tạo/duyệt/tham gia phòng và Ready | `UI_CoopMenu`, `CoopNetworkManager` |
| Host chọn nhiệm vụ; mỗi người chọn vũ khí; comic; Host Play | `CoopNetworkManager`, `UI`, `UI_MissionSelection`, `UI_WeaponSelection` |
| Di chuyển/chạy, ngắm, bắn, nạp và đổi vũ khí | `Player_Movement`, `Player_AimController`, `Player_WeaponController`, `NetworkPlayerWeapon` |
| Nhặt súng/đạn theo tương tác có điều kiện | `Player_Interaction`, `Pickup_Weapon`, `Pickup_Ammo`, `CoopPickupCoordinator` |
| Các biến thể nhiệm vụ và sử dụng xe | `Mission_*`, `MissionObject_*`, `Car_Interaction`, `Car_Controller` |
| Pause, chiến thắng, Game Over, chơi lại/về menu | `UI`, `GameManager`, `CoopPostMatchFlow`, `CoopTeamDeathHandler` |

> Phạm vi đúng với code hiện tại: Co-op dùng Host/Client; Host chọn nhiệm vụ và bắt đầu trận; mỗi người chọn bộ vũ khí; Host phân xử yêu cầu nhặt vật phẩm Co-op hợp lệ đầu tiên.
