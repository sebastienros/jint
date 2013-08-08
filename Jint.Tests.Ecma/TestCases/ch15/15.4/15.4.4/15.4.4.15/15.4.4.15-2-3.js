/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-3.js
 * @description Array.prototype.lastIndexOf - 'length' is own data property that overrides an inherited data property on an Array-like object
 */


function testcase() {

        var proto = {length: 0};

        var Con = function () {};
        Con.prototype = proto;

        var child = new Con();
        child.length = 2;
        child[1] = child;

        return Array.prototype.lastIndexOf.call(child, child) === 1;
    }
runTestCase(testcase);
