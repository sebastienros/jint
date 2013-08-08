/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-11.js
 * @description Object.defineProperties - 'P' is inherited accessor property without a get function (8.12.9 step 1 ) 
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "prop", {
            set: function () { },
            configurable: false
        });
        var Con = function () { };
        Con.prototype = proto;

        var obj = new Con();

        Object.defineProperties(obj, {
            prop: {
                get: function () {
                    return 12;
                },
                configurable: true
            }
        });
        return obj.hasOwnProperty("prop") && obj.prop === 12;

    }
runTestCase(testcase);
