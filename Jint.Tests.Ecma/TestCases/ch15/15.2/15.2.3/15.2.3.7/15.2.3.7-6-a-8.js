/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-8.js
 * @description Object.defineProperties - 'P' is own accessor property that overrides an inherited accessor property (8.12.9 step 1 ) 
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "prop", {
            get: function() {
                return 11;
            },
            configurable: true
        });
        var Con = function () { };
        Con.prototype = proto;

        var obj = new Con();
        Object.defineProperty(obj, "prop", {
            get: function () {
                return 12;
            },
            configurable: false
        });

        try {
            Object.defineProperties(obj, {
                prop: {
                    value: 13,
                    configurable: true
                }
            });
            return false;
        } catch (e) {
            return (e instanceof TypeError) && obj.prop === 12;
        }
    }
runTestCase(testcase);
