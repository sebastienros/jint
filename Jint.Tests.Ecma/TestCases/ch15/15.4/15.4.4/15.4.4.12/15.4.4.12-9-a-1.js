/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.12/15.4.4.12-9-a-1.js
 * @description Array.prototype.splice - 'from' is the result of ToString(actualStart+k) in an Array
 */


function testcase() {
        var arrObj = [1, 2, 3];
        var newArrObj = arrObj.splice(-2, 1);
        return newArrObj.length === 1 && newArrObj[0] === 2;
    }
runTestCase(testcase);
