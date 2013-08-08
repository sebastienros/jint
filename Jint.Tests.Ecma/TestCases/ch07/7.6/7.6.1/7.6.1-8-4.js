/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-4.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: new, var, catch
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set new(value){
                test0 = value;
            },
            get new(){
                return test0;
            },
            set var(value){
                test1 = value;
            },
            get var(){
                return test1;
            },
            set catch(value){
                test2 = value;
            },
            get catch(){
                return test2;
            }
        }; 
        var arr = [
            'new', 
            'var', 
            'catch'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
