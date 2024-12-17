import { inject, provideAppInitializer } from '@angular/core';
import { EmailSettingGroupComponent } from '../components/email-setting-group/email-setting-group.component';
import { eSettingManamagementSettingTabNames } from '../enums/setting-tab-names';
import { SettingTabsService } from '../services/settings-tabs.service';

export const SETTING_MANAGEMENT_SETTING_TAB_PROVIDERS = [
  provideAppInitializer(() => {
    configureSettingTabs(inject(SettingTabsService));
  }),
];

export function configureSettingTabs(settingTabs: SettingTabsService) {
  return () => {
    settingTabs.add([
      {
        name: eSettingManamagementSettingTabNames.EmailSettingGroup,
        order: 100,
        requiredPolicy: 'SettingManagement.Emailing',
        component: EmailSettingGroupComponent,
      },
    ]);
  };
}
