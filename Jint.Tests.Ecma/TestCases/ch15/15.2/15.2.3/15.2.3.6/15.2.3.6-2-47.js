/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-47.js
 * @description Object.defineProperty - TypeError exception is thrown  when 'P' is an object that neither toString nor valueOf returns a primitive value
 */


function testcase() {
        var obj = {};
        var toStringAccessed = false;
        var valueOfAccessed = false;

        var ownProp = {
            toString: function () {
                toStringAccessed = true;
                return {};
            },
            valueOf: function () {
                valueOfAccessed = true;
                return {};
            }
        };

        try {
            Object.defineProperty(obj, ownProp, {});
            return false;
        } catch (e) {
            return valueOfAccessed && toStringAccessed && e instanceof TypeError;
        }
    }
runTestCase(testcase);
