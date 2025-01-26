// 3D Cube Rotation
// http://www.speich.net/computer/moztesting/3d.htm
// Created by Simon Speich

const Q = [];
let MTrans = [];	// transformation matrix
let MQube = [];	// position information of qube
let I = [];			// entity matrix
const Origin = {};
const Testing = {};
let LoopTimer;

const DisplArea = {};
DisplArea.Width = 300;
DisplArea.Height = 300;

const DrawLine = (From, To) => {
    const x1 = From.V[0];
    const x2 = To.V[0];
    const y1 = From.V[1];
    const y2 = To.V[1];
    const dx = Math.abs(x2 - x1);
    const dy = Math.abs(y2 - y1);
    let x = x1;
    let y = y1;
    let IncX1, IncY1;
    let IncX2, IncY2;
    let Den;
    let Num;
    let NumAdd;
    let NumPix;

    if (x2 >= x1) { IncX1 = 1; IncX2 = 1; }
    else { IncX1 = -1; IncX2 = -1; }
    if (y2 >= y1) { IncY1 = 1; IncY2 = 1; }
    else { IncY1 = -1; IncY2 = -1; }
    if (dx >= dy) {
        IncX1 = 0;
        IncY2 = 0;
        Den = dx;
        Num = dx / 2;
        NumAdd = dy;
        NumPix = dx;
    }
    else {
        IncX2 = 0;
        IncY1 = 0;
        Den = dy;
        Num = dy / 2;
        NumAdd = dx;
        NumPix = dy;
    }

    NumPix = Math.round(Q.LastPx + NumPix);

    let i = Q.LastPx;
    for (; i < NumPix; i++) {
        Num += NumAdd;
        if (Num >= Den) {
            Num -= Den;
            x += IncX1;
            y += IncY1;
        }
        x += IncX2;
        y += IncY2;
    }
    Q.LastPx = NumPix;
};

const CalcCross = (V0, V1) => {
    const Cross = [];
    Cross[0] = V0[1] * V1[2] - V0[2] * V1[1];
    Cross[1] = V0[2] * V1[0] - V0[0] * V1[2];
    Cross[2] = V0[0] * V1[1] - V0[1] * V1[0];
    return Cross;
};

const CalcNormal = (V0, V1, V2) => {
    let A = [];
    const B = [];
    for (let i = 0; i < 3; i++) {
        A[i] = V0[i] - V1[i];
        B[i] = V2[i] - V1[i];
    }
    A = CalcCross(A, B);
    const Length = Math.sqrt(A[0] * A[0] + A[1] * A[1] + A[2] * A[2]);
    for (let i = 0; i < 3; i++) A[i] = A[i] / Length;
    A[3] = 1;
    return A;
};

const CreateP = function (X, Y, Z) {
    this.V = [X, Y, Z, 1];
};

// mulitplies two matrices
const MMulti = (M1, M2) => {
    const M = [[], [], [], []];
    let i = 0;
    let j = 0;
    for (; i < 4; i++) {
        j = 0;
        for (; j < 4; j++) M[i][j] = M1[i][0] * M2[0][j] + M1[i][1] * M2[1][j] + M1[i][2] * M2[2][j] + M1[i][3] * M2[3][j];
    }
    return M;
};

//multiplies matrix with vector
const VMulti = (M, V) => {
    const Vect = [];
    let i = 0;
    for (; i < 4; i++) Vect[i] = M[i][0] * V[0] + M[i][1] * V[1] + M[i][2] * V[2] + M[i][3] * V[3];
    return Vect;
};

const VMulti2 = (M, V) => {
    const Vect = [];
    let i = 0;
    for (; i < 3; i++) Vect[i] = M[i][0] * V[0] + M[i][1] * V[1] + M[i][2] * V[2];
    return Vect;
};

// add to matrices
const MAdd = (M1, M2) => {
    const M = [[], [], [], []];
    let i = 0;
    let j = 0;
    for (; i < 4; i++) {
        j = 0;
        for (; j < 4; j++) M[i][j] = M1[i][j] + M2[i][j];
    }
    return M;
};

const Translate = (M, Dx, Dy, Dz) => {
    const T = [
        [1, 0, 0, Dx],
        [0, 1, 0, Dy],
        [0, 0, 1, Dz],
        [0, 0, 0, 1]
    ];
    return MMulti(T, M);
};

const RotateX = (M, Phi) => {
    let a = Phi;
    a *= Math.PI / 180;
    const Cos = Math.cos(a);
    const Sin = Math.sin(a);
    const R = [
        [1, 0, 0, 0],
        [0, Cos, -Sin, 0],
        [0, Sin, Cos, 0],
        [0, 0, 0, 1]
    ];
    return MMulti(R, M);
};

const RotateY = (M, Phi) => {
    let a = Phi;
    a *= Math.PI / 180;
    const Cos = Math.cos(a);
    const Sin = Math.sin(a);
    const R = [
        [Cos, 0, Sin, 0],
        [0, 1, 0, 0],
        [-Sin, 0, Cos, 0],
        [0, 0, 0, 1]
    ];
    return MMulti(R, M);
};

const RotateZ = (M, Phi) => {
    let a = Phi;
    a *= Math.PI / 180;
    const Cos = Math.cos(a);
    const Sin = Math.sin(a);
    const R = [
        [Cos, -Sin, 0, 0],
        [Sin, Cos, 0, 0],
        [0, 0, 1, 0],
        [0, 0, 0, 1]
    ];
    return MMulti(R, M);
};

const DrawQube = () => {
    // calc current normals
    const CurN = [];
    let i = 5;
    Q.LastPx = 0;
    for (; i > -1; i--) CurN[i] = VMulti2(MQube, Q.Normal[i]);
    if (CurN[0][2] < 0) {
        if (!Q.Line[0]) { DrawLine(Q[0], Q[1]); Q.Line[0] = true; }
        if (!Q.Line[1]) { DrawLine(Q[1], Q[2]); Q.Line[1] = true; }
        if (!Q.Line[2]) { DrawLine(Q[2], Q[3]); Q.Line[2] = true; }
        if (!Q.Line[3]) { DrawLine(Q[3], Q[0]); Q.Line[3] = true; }
    }
    if (CurN[1][2] < 0) {
        if (!Q.Line[2]) { DrawLine(Q[3], Q[2]); Q.Line[2] = true; }
        if (!Q.Line[9]) { DrawLine(Q[2], Q[6]); Q.Line[9] = true; }
        if (!Q.Line[6]) { DrawLine(Q[6], Q[7]); Q.Line[6] = true; }
        if (!Q.Line[10]) { DrawLine(Q[7], Q[3]); Q.Line[10] = true; }
    }
    if (CurN[2][2] < 0) {
        if (!Q.Line[4]) { DrawLine(Q[4], Q[5]); Q.Line[4] = true; }
        if (!Q.Line[5]) { DrawLine(Q[5], Q[6]); Q.Line[5] = true; }
        if (!Q.Line[6]) { DrawLine(Q[6], Q[7]); Q.Line[6] = true; }
        if (!Q.Line[7]) { DrawLine(Q[7], Q[4]); Q.Line[7] = true; }
    }
    if (CurN[3][2] < 0) {
        if (!Q.Line[4]) { DrawLine(Q[4], Q[5]); Q.Line[4] = true; }
        if (!Q.Line[8]) { DrawLine(Q[5], Q[1]); Q.Line[8] = true; }
        if (!Q.Line[0]) { DrawLine(Q[1], Q[0]); Q.Line[0] = true; }
        if (!Q.Line[11]) { DrawLine(Q[0], Q[4]); Q.Line[11] = true; }
    }
    if (CurN[4][2] < 0) {
        if (!Q.Line[11]) { DrawLine(Q[4], Q[0]); Q.Line[11] = true; }
        if (!Q.Line[3]) { DrawLine(Q[0], Q[3]); Q.Line[3] = true; }
        if (!Q.Line[10]) { DrawLine(Q[3], Q[7]); Q.Line[10] = true; }
        if (!Q.Line[7]) { DrawLine(Q[7], Q[4]); Q.Line[7] = true; }
    }
    if (CurN[5][2] < 0) {
        if (!Q.Line[8]) { DrawLine(Q[1], Q[5]); Q.Line[8] = true; }
        if (!Q.Line[5]) { DrawLine(Q[5], Q[6]); Q.Line[5] = true; }
        if (!Q.Line[9]) { DrawLine(Q[6], Q[2]); Q.Line[9] = true; }
        if (!Q.Line[1]) { DrawLine(Q[2], Q[1]); Q.Line[1] = true; }
    }
    Q.Line = [false, false, false, false, false, false, false, false, false, false, false, false];
    Q.LastPx = 0;
};

const Loop = () => {
    if (Testing.LoopCount > Testing.LoopMax) return;
    let TestingStr = String(Testing.LoopCount);
    while (TestingStr.length < 3) TestingStr = "0" + TestingStr;
    MTrans = Translate(I, -Q[8].V[0], -Q[8].V[1], -Q[8].V[2]);
    MTrans = RotateX(MTrans, 1);
    MTrans = RotateY(MTrans, 3);
    MTrans = RotateZ(MTrans, 5);
    MTrans = Translate(MTrans, Q[8].V[0], Q[8].V[1], Q[8].V[2]);
    MQube = MMulti(MTrans, MQube);
    let i = 8;
    for (; i > -1; i--) {
        Q[i].V = VMulti(MTrans, Q[i].V);
    }
    DrawQube();
    Testing.LoopCount++;
    Loop();
};

function Init(CubeSize) {
    // init/reset vars
    Origin.V = [150, 150, 20, 1];
    Testing.LoopCount = 0;
    Testing.LoopMax = 50;
    Testing.TimeMax = 0;
    Testing.TimeAvg = 0;
    Testing.TimeMin = 0;
    Testing.TimeTemp = 0;
    Testing.TimeTotal = 0;
    Testing.Init = false;

    // transformation matrix
    MTrans = [
	[1, 0, 0, 0],
	[0, 1, 0, 0],
	[0, 0, 1, 0],
	[0, 0, 0, 1]
    ];

    // position information of qube
    MQube = [
	[1, 0, 0, 0],
	[0, 1, 0, 0],
	[0, 0, 1, 0],
	[0, 0, 0, 1]
    ];

    // entity matrix
    I = [
	[1, 0, 0, 0],
	[0, 1, 0, 0],
	[0, 0, 1, 0],
	[0, 0, 0, 1]
    ];

    // create qube
    Q[0] = new CreateP(-CubeSize, -CubeSize, CubeSize);
    Q[1] = new CreateP(-CubeSize, CubeSize, CubeSize);
    Q[2] = new CreateP(CubeSize, CubeSize, CubeSize);
    Q[3] = new CreateP(CubeSize, -CubeSize, CubeSize);
    Q[4] = new CreateP(-CubeSize, -CubeSize, -CubeSize);
    Q[5] = new CreateP(-CubeSize, CubeSize, -CubeSize);
    Q[6] = new CreateP(CubeSize, CubeSize, -CubeSize);
    Q[7] = new CreateP(CubeSize, -CubeSize, -CubeSize);

    // center of gravity
    Q[8] = new CreateP(0, 0, 0);

    // anti-clockwise edge check
    Q.Edge = [[0, 1, 2], [3, 2, 6], [7, 6, 5], [4, 5, 1], [4, 0, 3], [1, 5, 6]];

    // calculate squad normals
    Q.Normal = [];
    for (let i = 0; i < Q.Edge.length; i++) Q.Normal[i] = CalcNormal(Q[Q.Edge[i][0]].V, Q[Q.Edge[i][1]].V, Q[Q.Edge[i][2]].V);

    // line drawn ?
    Q.Line = [false, false, false, false, false, false, false, false, false, false, false, false];

    // create line pixels
    Q.NumPx = 9 * 2 * CubeSize;
    for (let i = 0; i < Q.NumPx; i++) new CreateP(0, 0, 0);

    MTrans = Translate(MTrans, Origin.V[0], Origin.V[1], Origin.V[2]);
    MQube = MMulti(MTrans, MQube);

    let i = 0;
    for (; i < 9; i++) {
        Q[i].V = VMulti(MTrans, Q[i].V);
    }
    DrawQube();
    Testing.Init = true;
    Loop();
}

startTest("dromaeo-3d-cube", '979cd0f1');

test("Rotate 3D Cube", () => Init(20));

endTest();