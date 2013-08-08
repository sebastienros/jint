/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-2-2.js
 * @description Array.isArray applied to an object with Array.prototype as the prototype
 */


function testcase() {

        var proto = Array.prototype;
        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        return !Array.isArray(child);
    }
runTestCase(testcase);
