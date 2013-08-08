/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-2-1.js
 * @description Array.isArray applied to an object with an array as the prototype
 */


function testcase() {

        var proto = [];
        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();
        return !Array.isArray(child);
    }
runTestCase(testcase);
