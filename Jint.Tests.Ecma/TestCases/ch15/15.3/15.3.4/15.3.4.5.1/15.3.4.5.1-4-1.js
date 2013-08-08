/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-1.js
 * @description [[Call]] - 'F''s [[BoundArgs]] is used as the former part of arguments of calling the [[Call]] internal method of 'F''s [[TargetFunction]] when 'F' is called
 */


function testcase() {
        var func = function (x, y, z) {
            return x + y + z;
        };

        var newFunc = Function.prototype.bind.call(func, {}, "a", "b", "c");

        return newFunc() === "abc";
    }
runTestCase(testcase);
