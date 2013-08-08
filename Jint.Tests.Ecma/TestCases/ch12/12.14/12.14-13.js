/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-13.js
 * @description catch introduces scope - updates are based on scope
 */


function testcase() {
        var res1 = false;
        var res2 = false;
        var res3 = false;

        var x_12_14_13 = 'local';
        try {
            function foo() {
                this.x_12_14_13 = 'instance';
            }

            try {
                throw foo;
            }
            catch (e) {
                res1 = (x_12_14_13 === 'local');
                e();
                res2 = (x_12_14_13 === 'local');
            }
            res3 = (x_12_14_13 === 'local');

            if (res1 === true &&
                res2 === true &&
                res3 === true) {
                return true;
            }
        } finally {
            delete this.x_12_14_13;
        }
    }
runTestCase(testcase);
