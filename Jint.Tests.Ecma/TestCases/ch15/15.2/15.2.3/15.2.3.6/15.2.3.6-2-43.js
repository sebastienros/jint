/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-43.js
 * @description Object.defineProperty - argument 'P' is an object that has an own toString method
 */


function testcase() {
        var obj = {};

        var ownProp = {
            toString: function () {
                return "abc";
            }
        };

        Object.defineProperty(obj, ownProp, {});

        return obj.hasOwnProperty("abc");

    }
runTestCase(testcase);
