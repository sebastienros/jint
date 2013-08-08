/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-16.js
 * @description Exception object is a function which update in catch block, when an exception parameter is called as a function in catch block, global object is passed as the this value
 */


function testcase() {
        try {
            throw function () {
                this._12_14_16_foo = "test";
            };
            return false;
        } catch (e) {
            var obj = {};
            obj.test = function () {
                this._12_14_16_foo = "test1";
            };
            e = obj.test;
            e();
            return fnGlobalObject()._12_14_16_foo === "test1";
        }
        finally {
            delete fnGlobalObject()._12_14_16_foo;
        }

    }
runTestCase(testcase);
