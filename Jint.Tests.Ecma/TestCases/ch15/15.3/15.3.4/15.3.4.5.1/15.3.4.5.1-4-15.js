/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5.1/15.3.4.5.1-4-15.js
 * @description [[Call]] - length of parameters of 'target' is 1, length of 'boundArgs' is 2, length of 'ExtraArgs' is 0, and with 'boundThis'
 */


function testcase() {
        var obj = { prop: "abc" };

        var func = function (x) {
            return this === obj && x === 1 && arguments[1] === 2 &&
                arguments[0] === 1 && arguments.length === 2 && this.prop === "abc";
        };

        var newFunc = Function.prototype.bind.call(func, obj, 1, 2);

        return newFunc();
    }
runTestCase(testcase);
