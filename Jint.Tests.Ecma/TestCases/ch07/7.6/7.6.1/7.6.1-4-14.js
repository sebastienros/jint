/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-4-14.js
 * @description Allow reserved words as property names by set function within an object, verified with hasOwnProperty: public, yield, interface
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
        for(var p in tokenCodes) {       
            for(var p1 in arr) {                
                if(arr[p1] === p) {
                    if(!tokenCodes.hasOwnProperty(arr[p1])) {
                        return false;
                    };
                }
            }
        }
        return true;
    }
runTestCase(testcase);
