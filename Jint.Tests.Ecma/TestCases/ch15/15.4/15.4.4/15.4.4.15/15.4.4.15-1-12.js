/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-12.js
 * @description Array.prototype.lastIndexOf applied to RegExp object
 */


function testcase() {

        var obj = new RegExp("afdasf");
        obj.length = 100;
        obj[1] = "afdasf";

        return Array.prototype.lastIndexOf.call(obj, "afdasf") === 1;
    }
runTestCase(testcase);
