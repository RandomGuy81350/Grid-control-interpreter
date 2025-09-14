# Grid-control-interpreter
It's a programming language interpreter allowing manipulation of cells in a 32×32 grid. Interpreter written in C#. Example script provided.
Available functions examples:

let name = variable; [creates variable]

let arrayname = {1,2,e,red,green,etc}; 
[creates array, can store string, no need to write string inside " ".]

let a= b-6; [creates variable a with value b-6]

C(x,y).visible=x; [x∈{true,false,flip}, true: makes cell at column x and row y visible, false: makes cell at (x,y) not visible(" ") character, flip: toggles the visibility state of cell (x,y)]

if(condition) { } else { }. supported. [supported conditons: ">", "<", ">=", "<=", "!=", "=="]

loop(times, delay) { }. [runs the lines inside loop{...} with x number of times and with delay of y milliseconds in the beggining of loop in loop(x,y){...} ]

while(condition){ } supported. [keeps running the lines inside the while{} without delay till condition is true. Delay still wouldn't occur if while is placed inside loop{}. while(){} loops without delays]

C(x,y).color=x; [x∈{red,blue,cyan,magenta,gray,green,white,black,yellow}. Sets the color of (x,y) to x color]

Crange(x1,y1,x2,y2) controls cells within a rectangular region. All controls (".visible.", ".color") are possible with Crange. Draws are slow. Crange must have 4 arguments.

Crange2(x1,y1,x2,y2). modified, optimised version of Crange, draws are fast. Same controls are Crange are applicable with Crange2. Crange2 must have 4 arguments.

wait(x); makes the program wait for x milliseconds and run the next line. Can be also used inside loops.

let a=irandom(x,y); [Creates a variable "a" with a random integer value x:x∈[x,y]] 

let a=1;
let b=1;
let c=3;
a+=c;
a-=b; 
supported.

let ar={1,2,red,yellow};
C(2,5).color=ar[3];
↑Sets cell (2,5) color to red.
C(3,5).color=ar[2];
↑Sets cell (3,5) color to white as "2" is not a color.
C(4,5).color=ar[5];
↑Sets cell (4,5) color to white as ar[5] does not exist.

Can start a line with "//" and the interpreter wouldn't read that line.

*Must write each line of command in one line and the other command in next line.
