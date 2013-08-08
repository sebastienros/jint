/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-8.js
 * @description [[Call]] - length of parameters of 'target' is 0, length of 'boundArgs' is 0, length of 'ExtraArgs' is 1, and with 'boundThis'
 */


function testcase() {
        var obj = { prop: "abc" };

        var func = function () {
            return this === obj && arguments[0] === 1;
        };

        var newFunc = Function.prototype.bind.call(func, obj);

        return newFunc(1);
    }
runTestCase(testcase);
