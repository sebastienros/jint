/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-12.js
 * @description Array.prototype.indexOf applied to RegExp object
 */


function testcase() {

        var obj = new RegExp();
        obj.length = 2;
        obj[1] = true;

        return Array.prototype.indexOf.call(obj, true) === 1;
    }
runTestCase(testcase);
