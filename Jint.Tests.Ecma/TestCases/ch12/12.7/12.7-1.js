/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.7/12.7-1.js
 * @description The continue Statement - a continue statement without an identifier may have a LineTerminator before the semi-colon
 */


function testcase() {
        var sum = 0;
        for (var i = 1; i <= 10; i++) {
            continue
            ;
            sum += i;
        }
        return sum === 0;
    }
runTestCase(testcase);
