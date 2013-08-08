/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-9.js
 * @description [[Construct]] - length of parameters of 'target' is 1, length of 'boundArgs' is 0, length of 'ExtraArgs' is 0
 */


function testcase() {
        var func = function (x) {
            return new Boolean(arguments.length === 0 && typeof x === "undefined");
        };

        var NewFunc = Function.prototype.bind.call(func, {});

        var newInstance = new NewFunc();

        return newInstance.valueOf() === true;
    }
runTestCase(testcase);
