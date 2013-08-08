/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5.2/15.3.4.5.2-4-5.js
 * @description [[Construct]] - length of parameters of 'target' is 0, length of 'boundArgs' is 0, length of 'ExtraArgs' is 0, and with 'boundThis'
 */


function testcase() {
        var obj = { prop: "abc" };

        try {
            Object.prototype.verifyThis = "verifyThis";
            var func = function () {
                return new Boolean(arguments.length === 0 && Object.prototype.toString.call(this) === "[object Object]" &&
                    this.verifyThis === "verifyThis");
            };

            var NewFunc = Function.prototype.bind.call(func, obj);

            var newInstance = new NewFunc();

            return newInstance.valueOf();
        } finally {
            delete Object.prototype.verifyThis;
        }
    }
runTestCase(testcase);
