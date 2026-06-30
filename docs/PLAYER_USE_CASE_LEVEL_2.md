# Sơ đồ Use Case của người chơi

Sơ đồ được giới hạn ở **2 mức**: chức năng tổng quát và chức năng con trực tiếp.

```mermaid
flowchart LR
    PLAYER["👤<br/>NGƯỜI CHƠI"]

    subgraph SYSTEM["HỆ THỐNG TOP DOWN SHOOTER 3D"]
        direction TB

        SETUP(["Chuẩn bị trận đấu"])
        COMBAT(["Tham gia chiến đấu"])
        COOP(["Chơi Co-op LAN"])
        RESULT(["Xử lý kết quả trận đấu"])

        CHOOSE_MISSION(["Chọn nhiệm vụ"])
        CHOOSE_WEAPON(["Chọn tối đa 2 vũ khí"])
        VIEW_COMIC(["Xem truyện mở đầu"])

        MOVE(["Di chuyển và ngắm bắn"])
        ATTACK(["Tấn công kẻ địch"])
        DO_MISSION(["Thực hiện mục tiêu nhiệm vụ"])
        PICKUP(["Nhặt vũ khí hoặc đạn"])
        DRIVE(["Sử dụng phương tiện"])
        PAUSE(["Tạm dừng và cài đặt"])

        ENTER_NAME(["Nhập tên hiển thị"])
        ROOM(["Vào phòng chơi"])
        HOST_ROOM(["Tạo phòng"])
        JOIN_ROOM(["Tham gia phòng"])
        READY(["Sẵn sàng"])
        TEAM_PLAY(["Phối hợp cùng đồng đội"])

        VIEW_RESULT(["Xem thắng hoặc Game Over"])
        REPLAY(["Chơi lại"])
        BACK_MENU(["Trở về menu"])

        SETUP -. "«include»" .-> CHOOSE_MISSION
        SETUP -. "«include»" .-> CHOOSE_WEAPON
        VIEW_COMIC -. "«extend»" .-> SETUP

        COMBAT -. "«include»" .-> MOVE
        COMBAT -. "«include»" .-> ATTACK
        COMBAT -. "«include»" .-> DO_MISSION
        PICKUP -. "«extend»" .-> COMBAT
        DRIVE -. "«extend»" .-> COMBAT
        PAUSE -. "«extend»" .-> COMBAT

        COOP -. "«include»" .-> ENTER_NAME
        COOP -. "«include»" .-> ROOM
        COOP -. "«include»" .-> READY
        COOP -. "«include»" .-> TEAM_PLAY
        HOST_ROOM -. "«extend»" .-> ROOM
        JOIN_ROOM -. "«extend»" .-> ROOM

        RESULT -. "«include»" .-> VIEW_RESULT
        REPLAY -. "«extend»" .-> RESULT
        BACK_MENU -. "«extend»" .-> RESULT
    end

    PLAYER --> SETUP
    PLAYER --> COMBAT
    PLAYER --> COOP
    PLAYER --> RESULT

    classDef actor fill:#263238,color:#fff,stroke:#111,stroke-width:2px;
    classDef level1 fill:#1565c0,color:#fff,stroke:#0d47a1,stroke-width:2px;
    classDef level2 fill:#e3f2fd,color:#102027,stroke:#42a5f5,stroke-width:1.5px;
    class PLAYER actor;
    class SETUP,COMBAT,COOP,RESULT level1;
    class CHOOSE_MISSION,CHOOSE_WEAPON,VIEW_COMIC,MOVE,ATTACK,DO_MISSION,PICKUP,DRIVE,PAUSE,ENTER_NAME,ROOM,HOST_ROOM,JOIN_ROOM,READY,TEAM_PLAY,VIEW_RESULT,REPLAY,BACK_MENU level2;
```

## Quy ước

- `«include»`: chức năng bắt buộc hoặc luôn được sử dụng trong use case chính.
- `«extend»`: chức năng tùy chọn hoặc chỉ xảy ra khi có điều kiện.
- Xanh đậm: use case mức 1; xanh nhạt: use case mức 2.
