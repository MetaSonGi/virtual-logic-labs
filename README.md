> 🔌 **디지털 논리회로 가상 실험실** — 브라우저에서 바로 브레드보드에 칩을 꽂고 배선하며 논리 게이트 실험을 해볼 수 있습니다.

---
# Virtual Logic Labs (Web)

2018년 Rutgers 대학교 ECE 학과 소프트웨어 공학 프로젝트 **Virtual Logic Labs**를 Unity 2017.3 → **Unity 6 (6000.3)** 로 올리고 **WebGL**로 빌드해 웹에서 실행되도록 만든 버전입니다. 설치 없이 브라우저에서 바로 실행됩니다.

🌐 **바로 실행:** https://metasongi.github.io/virtual-logic-labs/

## 로그인
| 구분 | 아이디 | 비밀번호 |
|---|---|---|
| 학생 (Student Subsystem) | `student` | `student` |
| 관리자 (Admin Subsystem) | `admin` | `admin` |

원래 쓰던 온라인 DB(AWS)는 더 이상 동작하지 않아서 위 계정으로만 들어갈 수 있습니다.

## 실험 구성
| 장면 | 설명 |
|---|---|
| `Login` | 로그인 화면 |
| `StudentSubsystem` | 실험 선택 메뉴 |
| `SandboxLab` | 자유 실험 — AND·OR·NAND·NOR·XOR·INV·MUX 칩과 LED·스위치 배치 |
| `Prelab1` → `Lab1` → `Postlab1` | 실험 1 사전 학습 · 실습 · 채점 |
| `Prelab2` → `Lab2` → `Postlab2` | 실험 2 (K-map·가감산기) |

## 폴더 구성
| 경로 | 설명 |
|---|---|
| `docs/` | WebGL 빌드 결과물 (GitHub Pages가 이 폴더를 서비스) |
| `Assets/` | Unity 프로젝트 소스 — 장면·스크립트·모델 |
| `Assets/Editor/WebGLBuilder.cs` | 메뉴 **Build → WebGL (docs)** 로 `docs/` 에 다시 빌드 |
| `ProjectSettings/`, `Packages/` | Unity 6 프로젝트 설정 |

## 다시 빌드하기
1. Unity Hub에서 이 폴더를 Unity **6000.3.21f1** 로 엽니다.
2. 메뉴 **Build → WebGL (docs)** 를 누르면 `docs/` 가 새로 만들어집니다.
3. 커밋·푸시하면 GitHub Pages에 반영됩니다.

## 참고
- 웹 버전에서는 웹캠 기능과 온라인 DB 계정 생성은 동작하지 않습니다.
- 원작: Rutgers ECE 2018 Spring Group 10 — Khalid Akash, Vikas Khan, Dhruvik Patel, Joe Cella, Sagar Phanda, Yiwen Tao. ([데모 영상](https://www.youtube.com/watch?v=CsVZMjCCOYc))
