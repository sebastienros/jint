/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-8-14.js
 * @description Allow reserved words as property names by set function within an object, accessed via indexing: public, yield, interface
 */


function testcase() {
        var test0 = 0, test1 = 1, test2 = 2;
        var tokenCodes  = {
            set public(value){
                test0 = value;
            },
            get public(){
                return test0;
            },
            set yield(value){
                test1 = value;
            },
            get yield(){
                return test1;
            },
            set interface(value){
                test2 = value;
            },
            get interface(){
                return test2;
            }
        }; 
        var arr = [
            'public',
            'yield',
            'interface'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
