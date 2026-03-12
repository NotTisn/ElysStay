# Hệ thống Quản lý Nhà trọ — Tài liệu dự án

> **Cập nhật:** 01-03-2026 (v3.0)

---

## Dự án này là gì?

API quản lý nhà trọ cho chủ nhà: tòa nhà, phòng, khách thuê, hợp đồng, chỉ số điện/nước, hóa đơn hàng tháng, thanh toán, chi phí, sự cố bảo trì, và báo cáo tài chính.

**Tech stack:** ASP.NET Core 8, C#, EF Core 8, PostgreSQL, JWT  
**Đội:** 3 dev (A, B, C) — 84 endpoint, 17 entity, 5 sprint

---

## Chia sẻ cho team: 4 file

| File | Xem khi | Nội dung |
|------|---------|----------|
| **endpoints.csv** | Cần biết phải code gì | 84 endpoint: sprint, dev, route, mô tả, phân quyền, quy tắc |
| **entities.csv** | Cần biết data model | 17 entity, từng field: kiểu, nullable, ràng buộc, ghi chú |
| **business-rules.csv** | Cần biết logic nghiệp vụ | 89 quy tắc: chuyển trạng thái, validation, tác vụ nền, thông báo |
| **api-spec.md** | Cần hiểu thiết kế tổng thể | Kiến trúc, state machine, thuật toán, pattern chung *(tiếng Anh)* |

---

## Cách tìm việc của mình

1. Mở **endpoints.csv** trong Excel
2. Lọc cột **Dev** theo chữ cái của mình (A, B, hoặc C)
3. Lọc cột **Sprint** theo sprint hiện tại
4. Mỗi dòng = 1 endpoint cần code
5. Cột **Quy tắc chính** = những gì endpoint phải xử lý
6. Cần chi tiết hơn → xem **business-rules.csv** (lọc theo Mã quy tắc được nhắc đến)

---

## Phân công tổng hợp

| Dev | Tổng | Sprint 1 | Sprint 2 | Sprint 3 | Sprint 4 | Sprint 5 |
|-----|------|----------|----------|----------|----------|----------|
| A | 31 | 7 | 9 | 4 | 3 | 8 |
| B | 22 | 4 | 6 | 3 | 4 | 5 |
| C | 31 | 5 | 7 | 6 | 8 | 5 |
| **Tổng** | **84** | **16** | **22** | **13** | **15** | **18** |

---

## Sprint tổng quan

| Sprint | Tên | Endpoint | Trọng tâm |
|--------|-----|----------|-----------|
| 1 | Xác thực & Người dùng | 16 | Đăng nhập/đăng ký, JWT, profile, dashboard |
| 2 | Tòa nhà & Phòng | 22 | CRUD tòa nhà, phòng, dịch vụ, phân công NV, cấu hình phí |
| 3 | Hồ sơ & Hợp đồng | 13 | CCCD/OCR, đặt cọc, hợp đồng, thanh lý/gia hạn |
| 4 | Chỉ số & Hóa đơn | 15 | Nhập chỉ số điện/nước, tạo/gửi hóa đơn, xuất PDF |
| 5 | Thanh toán & Báo cáo | 18 | Ghi thanh toán, chi phí, sự cố, dashboard, báo cáo lãi/lỗ |

---

## 3 vai trò (role)

| Role | Tên | Quyền chính |
|------|-----|-------------|
| OWNER | Chủ nhà | Toàn quyền: tạo/xóa tòa nhà, phòng, NV, hóa đơn, báo cáo lãi/lỗ |
| STAFF | Nhân viên (NV) | Quản lý phòng/khách/HĐ/chỉ số/hóa đơn trong tòa nhà được phân công. Không xóa tài chính |
| TENANT | Khách thuê (KT) | Xem phòng/HĐ/hóa đơn của mình, báo sự cố, nhận thông báo |

---

## Viết tắt trong CSV

| Viết tắt | Nghĩa |
|----------|-------|
| KT | Khách thuê (TENANT) |
| NV | Nhân viên (STAFF) |
| HĐ | Hợp đồng |
| HĐơn | Hóa đơn |
| DV | Dịch vụ |
| SĐT | Số điện thoại |
| CCCD | Căn cước công dân |

---

## Quy tắc khi có mâu thuẫn giữa các file

```
api-spec.md (thiết kế) > endpoints.csv / entities.csv / business-rules.csv
```

Nếu CSV nói khác api-spec.md → **sửa CSV** cho khớp api-spec.md.

---

## Cách review (quay lại sau 1 tuần)

1. Đọc README này trước
2. Mở **api-spec.md** → đọc §3 (state machine) và §6 (thuật toán) — phần khó nhất
3. Kiểm tra chéo: cột **Phân quyền** trong endpoints.csv khớp **§12** (bảng quyền) trong api-spec.md?
4. Kiểm tra: mỗi quy tắc trong business-rules.csv có endpoint tương ứng?

---

## Cách cập nhật

| Thay đổi | Làm gì |
|----------|--------|
| Thêm endpoint | Thêm dòng trong endpoints.csv. Nếu thay đổi kiến trúc → cập nhật api-spec.md |
| Thêm entity/field | Thêm dòng trong entities.csv. Nếu thêm quan hệ → cập nhật sơ đồ api-spec.md §2 |
| Thêm quy tắc | Thêm dòng trong business-rules.csv. Nếu là thuật toán → cập nhật api-spec.md §6 |
| Quyết định thiết kế | Thêm vào api-spec.md §13 để không ai hỏi lại "tại sao" |

---

## Lịch sử phiên bản

| Phiên bản | Ngày | Thay đổi |
|-----------|------|----------|
| 1.0 | 02-2026 | Bản đầu từ rent.csv. 66 endpoint, 17 entity |
| 2.0 | 02-2026 | 44 lỗi thiết kế. Lên 84 endpoint, thêm RoomService |
| 2.1 | 02-2026 | 10 sửa thêm, 4 false positive bỏ |
| 3.0 | 01-03-2026 | Tách rõ: spec (thiết kế tiếng Anh) vs CSV (sản phẩm tiếng Việt). README tiếng Việt |

---

## Kiểm tra nhanh (số liệu đã xác minh bằng script)

| Chỉ số | Giá trị |
|--------|---------|
| Tổng endpoint | 84 |
| Tổng entity | 17 |
| Số sprint | 5 |
| Dev A | 31 (Sp1:7, Sp2:9, Sp3:4, Sp4:3, Sp5:8) |
| Dev B | 22 (Sp1:4, Sp2:6, Sp3:3, Sp4:4, Sp5:5) |
| Dev C | 31 (Sp1:5, Sp2:7, Sp3:6, Sp4:8, Sp5:5) |
| Quy tắc nghiệp vụ | 89 (17 nhóm) |
| Câu hỏi còn mở | 3 (xem api-spec.md §15) |
