/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.9/12.9-1.js
 * @description The return Statement - a return statement without an expression may have a LineTerminator before the semi-colon
 */


function testcase() {
        var sum = 0;
        (function innerTest() {
            for (var i = 1; i <= 10; i++) {
                if (i === 6) {
                    return
                    ;
                }
                sum += i;
            }
        })();
        return sum === 15;
    }
runTestCase(testcase);
