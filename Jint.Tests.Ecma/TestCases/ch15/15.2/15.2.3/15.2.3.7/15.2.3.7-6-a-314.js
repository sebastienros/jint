/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-314.js
 * @description Object.defineProperties - 'O' is an Arguments object, 'name' is own property of [[ParameterMap]] of 'O', test 'name' is deleted if 'name' is configurable and 'desc' is accessor descriptor (10.6 [[DefineOwnProperty]] step 5.a.i)
 */


function testcase() {
        var arg = (function () {
            return arguments;
        }(1, 2, 3));
        var accessed = false;

        Object.defineProperties(arg, {
            "0": {
                get: function () {
                    accessed = true;
                    return 12;
                }
            }
        });

        return arg[0] === 12 && accessed;
    }
runTestCase(testcase);
