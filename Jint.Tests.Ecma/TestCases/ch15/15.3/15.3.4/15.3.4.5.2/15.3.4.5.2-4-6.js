/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-6.js
 * @description [[Construct]] - length of parameters of 'target' is 0, length of 'boundArgs' is 1, length of 'ExtraArgs' is 0
 */


function testcase() {
        var func = function () {
            return new Boolean(arguments.length === 1 && arguments[0] === 1);
        };

        var NewFunc = Function.prototype.bind.call(func, {}, 1);

        var newInstance = new NewFunc();

        return newInstance.valueOf() === true;
    }
runTestCase(testcase);
