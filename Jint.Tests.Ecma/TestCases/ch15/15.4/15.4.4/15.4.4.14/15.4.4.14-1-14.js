/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-14.js
 * @description Array.prototype.indexOf applied to Error object
 */


function testcase() {

        var obj = new SyntaxError();
        obj[1] = true;
        obj.length = 2;

        return Array.prototype.indexOf.call(obj, true) === 1;
    }
runTestCase(testcase);
