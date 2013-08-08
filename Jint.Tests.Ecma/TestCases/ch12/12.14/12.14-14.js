/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-14.js
 * @description Exception object is a function, when an exception parameter is called as a function in catch block, global object is passed as the this value
 */


function testcase() {
        try {
            throw function () {
                this._12_14_14_foo = "test";
            };
            return false;
        } catch (e) {
            e();
            return fnGlobalObject()._12_14_14_foo === "test";
        }
        finally {
           delete fnGlobalObject()._12_14_14_foo;
        }
    }
runTestCase(testcase);
