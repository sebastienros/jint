/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-3-1.js
 * @description Object.getOwnPropertyNames - elements of the returned array start from index 0
 */


function testcase() {
        var obj = { prop1: 1001 };

        var arr = Object.getOwnPropertyNames(obj);

        return arr.hasOwnProperty(0) && arr[0] === "prop1";
    }
runTestCase(testcase);
